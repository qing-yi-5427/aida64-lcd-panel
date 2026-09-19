package com.paneldeck.aida;

import java.io.ByteArrayOutputStream;
import java.io.EOFException;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;

/** Bounded framing. An incomplete event is never accepted as a fresh heartbeat. */
final class EventStream {
    static String readEvent(InputStream in) throws IOException {
        StringBuilder data = new StringBuilder();
        while (true) {
            ByteArrayOutputStream line = new ByteArrayOutputStream();
            int b;
            while ((b = in.read()) != -1 && b != '\n') {
                if (line.size() >= 128 * 1024) throw new IOException("Event too large");
                if (b != '\r') line.write(b);
            }
            if (b == -1) throw new EOFException("Event stream ended");
            String text = new String(line.toByteArray(), StandardCharsets.UTF_8);
            if (text.isEmpty() && data.length() > 0) return data.toString();
            if (text.startsWith("data:")) {
                String value = text.substring(5);
                if (value.startsWith(" ")) value = value.substring(1);
                if (data.length() + value.length() > 128 * 1024) throw new IOException("Event too large");
                if (data.length() > 0) data.append('\n');
                data.append(value);
            }
        }
    }
    static long retryDelay(int failures) { return Math.min(15000L, 500L << Math.min(5, Math.max(1, failures))); }
}
