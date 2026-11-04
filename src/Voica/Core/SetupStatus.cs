namespace Voica;

public enum SetupIssue { None, LocalModel, CloudKey, TranslationKey, TranslationModel, EditingKey, EditingModel, EditingPrompt }

/// <summary>Recognition prerequisites first, then enabled API stages. No network requests.</summary>
public sealed record SetupStatus
{
    public EngineKind Engine { get; init; }
    public bool ModelInstalled { get; init; }
    public bool HasCloudKey { get; init; }
    public bool TranslationEnabled { get; init; }
    public bool HasTranslationKey { get; init; }
    public string TranslationModel { get; init; } = "";
    public bool EditingEnabled { get; init; }
    public bool HasEditingKey { get; init; }
    public string EditingModel { get; init; } = "";
    public string EditingPrompt { get; init; } = "";

    public SetupIssue RecognitionIssue => Engine == EngineKind.Local && !ModelInstalled ? SetupIssue.LocalModel
        : Engine == EngineKind.Cloud && !HasCloudKey ? SetupIssue.CloudKey : SetupIssue.None;

    public SetupIssue PipelineIssue => TranslationEnabled && !HasTranslationKey ? SetupIssue.TranslationKey
        : TranslationEnabled && string.IsNullOrWhiteSpace(TranslationModel) ? SetupIssue.TranslationModel
        : EditingEnabled && !HasEditingKey ? SetupIssue.EditingKey
        : EditingEnabled && string.IsNullOrWhiteSpace(EditingModel) ? SetupIssue.EditingModel
        : EditingEnabled && string.IsNullOrWhiteSpace(EditingPrompt) ? SetupIssue.EditingPrompt : SetupIssue.None;

    public SetupIssue Issue => RecognitionIssue != SetupIssue.None ? RecognitionIssue : PipelineIssue;

    public static SetupStatus Read() => new()
    {
        Engine = Prefs.Engine,
        ModelInstalled = ModelManager.IsInstalled(),
        HasCloudKey = KeyStore.HasKey,
        TranslationEnabled = Prefs.TranslationEnabled,
        HasTranslationKey = !string.IsNullOrWhiteSpace(TranslatorKeyStore.Load("translation", Prefs.TranslationProvider)),
        TranslationModel = Prefs.TranslationModel,
        EditingEnabled = Prefs.CleanupEnabled,
        HasEditingKey = !string.IsNullOrWhiteSpace(TranslatorKeyStore.Load("cleanup", Prefs.CleanupProvider)),
        EditingModel = Prefs.CleanupModel,
        EditingPrompt = Prefs.CleanupPrompt,
    };

    public static int Section(SetupIssue issue) => issue is SetupIssue.LocalModel or SetupIssue.CloudKey ? 1 : 2;

    public static string Message(SetupIssue issue) => issue switch
    {
        SetupIssue.LocalModel => S.SetupLocalModel,
        SetupIssue.CloudKey => S.SetupCloudKey,
        SetupIssue.TranslationKey => S.SetupTranslationKey,
        SetupIssue.TranslationModel => S.SetupTranslationModel,
        SetupIssue.EditingKey => S.SetupEditingKey,
        SetupIssue.EditingModel => S.SetupEditingModel,
        SetupIssue.EditingPrompt => S.SetupEditingPrompt,
        _ => "",
    };
}
