using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;
using ArmZavuch.Services.Schedule;
using ArmZavuch.Services.Staff;

namespace ArmZavuch.Services.Scoring;

/// <summary>Скоринг кандидатов на замену (ТЗ §5).</summary>
public sealed class SubstitutionScorer
{
    private const int SubjectMatch = 100;
    private const int ClassSubjectMatch = 130;
    private const int HomeroomMatch = 50;
    private const int SameBuilding = 30;
    private const int AuxiliaryBase = 15;
    private const int HighDailyLoadLessonCount = 7;

    private readonly RoomRepository _rooms;
    private readonly TeacherAvailabilityService _availability;
    private readonly BuildingTransitionChecker _transitions;
    private readonly BellRepository _bells;

    public SubstitutionScorer(
        RoomRepository rooms,
        TeacherAvailabilityService availability,
        BuildingTransitionChecker transitions,
        BellRepository bells)
    {
        _rooms = rooms;
        _availability = availability;
        _transitions = transitions;
        _bells = bells;
    }

    public async Task<List<SubstitutionCandidate>> RankAsync(
        LessonSlot slot,
        List<LessonSlot> dayLessons,
        List<Teacher> allTeachers)
    {
        var targetBuilding = slot.BuildingName;
        var busyTeacherIds = dayLessons
            .Where(l => !l.IsCancelled && BellScheduleResolver.TimesOverlap(slot, l))
            .SelectMany(GetOccupyingTeacherIds)
            .ToHashSet();

        var rooms = await _rooms.GetAllAsync();
        var bells = await _bells.GetAllPeriodsAsync();
        var routeMap = await _transitions.LoadRouteMapAsync();
        var availabilityLesson = SubjectScheduleRules.IsDynamicPause(slot.SubjectName)
            ? slot.LessonNumber
            : ScheduleGridBuilder.ResolveLogicalLessonNumber(bells, slot);
        var candidates = new List<SubstitutionCandidate>();

        foreach (var teacher in allTeachers)
        {
            if (teacher.Id == slot.TeacherId)
                continue;

            if (!await _availability.IsAvailableForLessonAsync(teacher.Id, slot.Date, availabilityLesson))
                continue;

            var score = teacher.TeacherType switch
            {
                TeacherTypes.Auxiliary => AuxiliaryBase,
                TeacherTypes.Primary => 10,
                _ => 0
            };
            if (slot.SubjectId > 0
                && TeacherCurriculumMatcher.TeacherHasAssignment(teacher, slot.ClassId, slot.SubjectId))
                score += ClassSubjectMatch;
            if (teacher.PrimarySubject?.Equals(slot.SubjectName, StringComparison.OrdinalIgnoreCase) == true)
                score += SubjectMatch;
            else if (teacher.SecondarySubjects.Any(s =>
                         s.Equals(slot.SubjectName, StringComparison.OrdinalIgnoreCase)))
                score += SubjectMatch / 2;
            if (teacher.HomeroomClass?.Equals(slot.ClassName, StringComparison.OrdinalIgnoreCase) == true)
                score += HomeroomMatch;

            var currentLesson = dayLessons
                .Where(l => !l.IsCancelled && OccupiesTeacher(l, teacher.Id))
                .FirstOrDefault(l => BellScheduleResolver.TimesOverlap(slot, l));
            var isBusy = busyTeacherIds.Contains(teacher.Id);
            var location = "Свободен";
            string? freesAt = null;

            if (isBusy && currentLesson is not null)
            {
                location = $"{currentLesson.BuildingName}, каб. {currentLesson.RoomNumber}";
                freesAt = currentLesson.EndTime;
            }
            else
            {
                var prev = dayLessons
                    .Where(l => !l.IsCancelled && OccupiesTeacher(l, teacher.Id) && l.LessonNumber < slot.LessonNumber)
                    .OrderByDescending(l => l.LessonNumber)
                    .FirstOrDefault();
                if (prev is not null)
                {
                    location = $"{prev.BuildingName}, каб. {prev.RoomNumber}";
                    if (prev.BuildingName.Equals(targetBuilding, StringComparison.OrdinalIgnoreCase))
                        score += SameBuilding;
                }
                else
                {
                    score += SameBuilding;
                }
            }

            var probe = new LessonSlot
            {
                Date = slot.Date,
                DayOfWeek = slot.DayOfWeek,
                LessonNumber = slot.LessonNumber,
                ClassId = slot.ClassId,
                ClassName = slot.ClassName,
                ClassGrade = slot.ClassGrade,
                ClassShift = slot.ClassShift,
                TeacherId = teacher.Id,
                TeacherName = teacher.FullName,
                RoomId = slot.RoomId,
                RoomNumber = slot.RoomNumber,
                BuildingName = slot.BuildingName,
                SubgroupIndex = slot.SubgroupIndex
            };
            var transitionWarnings = _transitions.CheckTeacherDayWithSubstitutions(
                dayLessons, teacher.Id, probe, bells, routeMap);
            var lateRisk = transitionWarnings.Any(w =>
                w.IsTimeCritical || (w.IsConsecutiveDifferentBuildings && !w.IsShuttleReminder));
            var shuttleCount = transitionWarnings.Count(w => w.IsShuttleReminder);

            var lessonsToday = dayLessons.Count(l => !l.IsCancelled && OccupiesTeacher(l, teacher.Id));
            var loadWarning = lessonsToday > HighDailyLoadLessonCount;

            if (isBusy)
                continue;

            var dayFinished = TeacherDayFinishedDetector.Evaluate(teacher, slot, dayLessons, bells);

            candidates.Add(new SubstitutionCandidate
            {
                TeacherId = teacher.Id,
                TeacherName = teacher.FullName,
                Score = score,
                CurrentLocation = location,
                FreesAt = freesAt,
                HasLateRisk = lateRisk,
                HasShuttleWarning = shuttleCount > 0,
                ShuttleWarningText = FormatShuttleWarning(shuttleCount),
                CrossBuildingTransitionCount = shuttleCount,
                HasLoadWarning = loadWarning,
                LessonsToday = lessonsToday,
                Phone = teacher.Phone,
                ContactUrl = teacher.ContactUrl,
                RoleHint = teacher.JobTitle ?? teacher.TypeDisplay,
                IsDayFinished = dayFinished.IsDayFinished,
                LastLessonEndTime = dayFinished.LastLessonEndTime,
                StillWorkingHint = dayFinished.StillWorkingHint
            });
        }

        return candidates
            .OrderBy(candidate => candidate.IsDayFinished)
            .ThenByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.TeacherName)
            .ToList();
    }

    private static IEnumerable<int> GetOccupyingTeacherIds(LessonSlot lesson)
    {
        yield return lesson.TeacherId;
        if (lesson.HasAssignedReplacement && lesson.ReplacementTeacherId is int replacementId)
            yield return replacementId;
    }

    private static bool OccupiesTeacher(LessonSlot lesson, int teacherId) =>
        lesson.TeacherId == teacherId
        || (lesson.HasAssignedReplacement && lesson.ReplacementTeacherId == teacherId);

    private static string FormatShuttleWarning(int count) => count switch
    {
        0 => "",
        1 => "1 переход между зданиями",
        2 or 3 or 4 => $"{count} перехода между зданиями",
        _ => $"{count} переходов между зданиями"
    };
}
