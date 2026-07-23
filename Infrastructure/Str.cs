namespace ULM.Infrastructure
{
    // Ein Eintrag pro übersetzbarem Text im Programm. Phase 1 deckt nur den
    // Hauptfenster-Rahmen ab (siehe
    // docs/superpowers/specs/2026-07-22-bilingual-ui-infrastructure-design.md) —
    // weitere Phasen erweitern dieses enum um Dialoge und den
    // Log-/Aktivitätsverlauf.
    public enum Str
    {
        Tab_IsoSelection,
        Tab_Log,
        Tab_Status,
        Btn_Download,
        Btn_CheckForUpdates,
        Btn_Cancel,
        Btn_Help,
        Tooltip_ThemeToggle,
        Tooltip_LanguageToggle,
        LanguageChangeConfirm_Title,
        LanguageChangeConfirm_Message,
    }
}
