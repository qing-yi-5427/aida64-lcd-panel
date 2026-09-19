package com.paneldeck.aida;

import java.net.URI;

final class LanAddress {
    static String normalize(String raw) {
        try {
            String text = raw.trim();
            URI uri = new URI(text.contains("://") ? text : "http://" + text);
            if (!"http".equalsIgnoreCase(uri.getScheme()) || uri.getUserInfo() != null || uri.getQuery() != null || uri.getFragment() != null)
                throw new IllegalArgumentException();
            String path = uri.getPath();
            if (path != null && !path.isEmpty() && !"/".equals(path)) throw new IllegalArgumentException();
            String host = uri.getHost();
            if (host == null || !host.matches("[0-9.]+")) throw new IllegalArgumentException();
            String[] parts = host.split("\\.", -1);
            if (parts.length != 4) throw new IllegalArgumentException();
            int[] b = new int[4];
            for (int i = 0; i < 4; i++) { b[i] = Integer.parseInt(parts[i]); if (b[i] < 0 || b[i] > 255 || !parts[i].equals(Integer.toString(b[i]))) throw new IllegalArgumentException(); }
            if (!(b[0] == 10 || b[0] == 127 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254))) throw new IllegalArgumentException();
            int port = uri.getPort() == -1 ? 8080 : uri.getPort();
            if (port < 1024 || port > 65535) throw new IllegalArgumentException();
            return "http://" + host + ":" + port;
        } catch (Exception e) { throw new IllegalArgumentException("请输入局域网电脑地址，例如 http://192.168.1.20:8080"); }
    }
}
