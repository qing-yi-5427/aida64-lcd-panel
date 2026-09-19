package com.paneldeck.aida;

/** Local display preferences. Theme IDs also form part of the bundled panel's JS contract. */
final class PanelTheme {
    static final String KEY = "panel_theme";
    static final String LOCAL_URL = "file:///android_asset/index.html";
    static final String[] IDS = { "classic", "material", "winui", "flutter", "glass", "editorial", "ambient", "telemetry", "studio" };
    static final String[] NAMES = { "经典 · 仪表盘", "Material · 安卓", "WinUI · 微软", "Flutter · 清新卡片", "macOS · 玻璃质感", "纸页 · 数据周刊", "静夜 · 桌面时钟", "遥测 · 性能座舱", "拼贴 · 硬件工作室" };
    static final String[] DESCRIPTIONS = {
            "保留经典仪表布局，放大数据和辅助文字。",
            "柔和配色、圆角卡片，参考安卓 Material 设计。",
            "清晰分区、细腻描边，参考微软 WinUI 设计。",
            "轻盈卡片与鲜明重点色，呈现 Flutter 应用常见的现代风格。",
            "半透明层次与柔和光感，参考 macOS 玻璃质感。",
            "暖纸色与编辑式排版，硬件数据像章节一样纵向排列。支持横屏重排。",
            "深色背景和大时钟，硬件数据收纳为紧凑条目，适合夜晚桌面。支持横屏重排。",
            "工业遥测风格，CPU 与 GPU 各占一条全宽数据轨道。支持横屏重排。",
            "非对称拼贴，突出 GPU 数据，功耗和内存组成独立信息块。支持横屏重排。"
    };

    private PanelTheme() {}

    static String normalize(String value) {
        for (String id : IDS) if (id.equals(value)) return id;
        return "classic";
    }

    static int indexOf(String value) {
        String safe = normalize(value);
        for (int i = 0; i < IDS.length; i++) if (IDS[i].equals(safe)) return i;
        return 0;
    }

    static String url(String value) { return LOCAL_URL + "#theme=" + normalize(value); }

    static boolean isLocalUrl(String url) {
        return LOCAL_URL.equals(url) || url != null && url.startsWith(LOCAL_URL + "#");
    }
}
