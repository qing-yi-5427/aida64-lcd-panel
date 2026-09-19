package com.paneldeck.aida;

public final class PanelThemeTest {
    public static void main(String[] args) {
        int checks = 0;
        if (PanelTheme.IDS.length != PanelTheme.NAMES.length || PanelTheme.IDS.length != PanelTheme.DESCRIPTIONS.length)
            throw new AssertionError("Theme IDs, names and descriptions must correspond");
        checks++;
        for (String id : PanelTheme.IDS) {
            if (!PanelTheme.normalize(id).equals(id) || !PanelTheme.isLocalUrl(PanelTheme.url(id)))
                throw new AssertionError("Theme contract: " + id);
            checks++;
        }
        for (String bad : new String[] { null, "", "unknown", "');alert(1);//", "glass&token=secret" }) {
            if (!"classic".equals(PanelTheme.normalize(bad)) || !PanelTheme.url(bad).endsWith("#theme=classic"))
                throw new AssertionError("Unsafe theme value: " + bad);
            checks++;
        }
        for (String external : new String[] { null, "https://example.com/#theme=glass", "file:///android_asset/index.html.evil", "file:///android_asset/index.html/../other.html", "file:///android_asset/index.html?remote=true" }) {
            if (PanelTheme.isLocalUrl(external)) throw new AssertionError("External page accepted: " + external);
            checks++;
        }
        System.out.println("PASS: Theme whitelist, local URL recognition and invalid preference fallback (" + checks + " checks)");
    }
}
