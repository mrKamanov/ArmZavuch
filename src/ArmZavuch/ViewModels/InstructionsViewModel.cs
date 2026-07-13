using System.Collections.ObjectModel;
using ArmZavuch.Services.Help;
using ArmZavuch.Services.Navigation;
using ArmZavuch.Services.Update;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArmZavuch.ViewModels;

/// <summary>Вкладка «Инструкция»: дерево, поиск, статьи, переход в модули, проверка обновлений.</summary>
public partial class InstructionsViewModel : ObservableObject
{
    private readonly IModuleNavigationService _navigation;
    private readonly AppUpdateCoordinator _updates;
    private readonly InstructionCatalog _catalog = InstructionWiki.Catalog;

    public InstructionsViewModel(IModuleNavigationService navigation, AppUpdateCoordinator updates)
    {
        _navigation = navigation;
        _updates = updates;
        foreach (var node in InstructionTreeBuilder.Build(_catalog))
            TopicTree.Add(node);

        SelectArticle("start.roadmap");
    }

    public string VersionLabel => _updates.VersionLabel;

    public IReadOnlyList<InstructionQuickRoute> QuickRoutes => _catalog.QuickRoutes;

    public ObservableCollection<InstructionTopicNode> TopicTree { get; } = [];

    public ObservableCollection<InstructionArticle> SearchResults { get; } = [];

    [ObservableProperty]
    private InstructionArticle? _selectedArticle;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private bool _isSearchActive;

    public ObservableCollection<InstructionArticle> RelatedArticles { get; } = [];

    public bool HasRelatedArticles => RelatedArticles.Count > 0;

    public bool CanOpenModule => !string.IsNullOrEmpty(SelectedArticle?.ModuleKey);

    public string? OpenModuleLabel => SelectedArticle?.ModuleKey switch
    {
        "Dispatcher" => "Открыть диспетчерскую",
        "Constructor" => "Открыть конструктор",
        "Directories" => "Открыть справочники",
        "Overview" => "Открыть сводку",
        "Rooms" => "Открыть кабинеты",
        _ => null
    };

    partial void OnSearchQueryChanged(string value)
    {
        SearchResults.Clear();
        IsSearchActive = !string.IsNullOrWhiteSpace(value);
        if (!IsSearchActive)
        {
            OnPropertyChanged(nameof(ShowSearchEmpty));
            return;
        }

        foreach (var hit in InstructionWiki.Search(value))
            SearchResults.Add(hit);

        OnPropertyChanged(nameof(ShowSearchEmpty));
    }

    public bool ShowSearchEmpty => IsSearchActive && SearchResults.Count == 0;

    partial void OnSelectedArticleChanged(InstructionArticle? value)
    {
        RelatedArticles.Clear();
        if (value is null)
        {
            OnPropertyChanged(nameof(CanOpenModule));
            OnPropertyChanged(nameof(OpenModuleLabel));
            OnPropertyChanged(nameof(HasRelatedArticles));
            return;
        }

        foreach (var id in value.RelatedIds)
        {
            if (_catalog.ById.TryGetValue(id, out var related) && !related.IsGroup)
                RelatedArticles.Add(related);
        }

        OnPropertyChanged(nameof(CanOpenModule));
        OnPropertyChanged(nameof(OpenModuleLabel));
        OnPropertyChanged(nameof(HasRelatedArticles));
    }

    [RelayCommand]
    private void SelectArticle(string? articleId)
    {
        if (string.IsNullOrEmpty(articleId) || !_catalog.ById.TryGetValue(articleId, out var article) || article.IsGroup)
            return;

        SelectedArticle = article;
    }

    [RelayCommand]
    private void SelectTopic(InstructionTopicNode? node)
    {
        if (node?.Article is not null)
            SelectedArticle = node.Article;
    }

    [RelayCommand]
    private void OpenModule()
    {
        if (!string.IsNullOrEmpty(SelectedArticle?.ModuleKey))
            _navigation.GoTo(SelectedArticle.ModuleKey);
    }

    [RelayCommand]
    private void ClearSearch() => SearchQuery = "";

    [RelayCommand]
    private async Task CheckForUpdatesAsync() => await _updates.CheckManuallyAsync();
}
