package com.paneldeck.aida;
public final class DesktopPolicyTest {
    private static void check(String expected, String actual) { if (!expected.equals(actual)) throw new AssertionError(expected + " != " + actual); }
    public static void main(String[] args) throws Exception {
        check("on", DesktopScreenPolicy.decide(false, false));
        check("off", DesktopScreenPolicy.decide(true, false));
        check("off", DesktopScreenPolicy.decide(false, true));
        check("off", DesktopScreenPolicy.decide(true, true));
        check("on", DesktopScreenPolicy.connectionMode(46000, 1000, 1000, 0, "on"));
        check("off", DesktopScreenPolicy.connectionMode(46001, 1000, 1000, 0, "on"));
        check("off", DesktopScreenPolicy.connectionMode(20000, 1000, 1000, 0, "off"));
        check("on", DesktopScreenPolicy.connectionMode(47000, 46001, 1000, 0, "on"));
        check("on", DesktopScreenPolicy.connectionMode(47000, 1000, 1000, 50000, "off"));
        check("off", DesktopScreenPolicy.connectionMode(50001, 1000, 1000, 50000, "off"));
        check("http://192.168.1.20:8080", LanAddress.normalize("192.168.1.20"));
        check("http://10.0.0.2:18764", LanAddress.normalize("http://10.0.0.2:18764/"));
        for (String bad : new String[] {"http://8.8.8.8:8080", "file:///data/local", "http://192.168.1.20:8080@8.8.8.8", "http://192.168.1.20:8080/path", "http://192.168.1.20:8080?token=x", "http://192.168.999.1:8080", "http://192.168.1.20:80", "http://192.168.1.020:8080"}) {
            try { LanAddress.normalize(bad); throw new AssertionError("Accepted: " + bad); } catch (IllegalArgumentException expected) { }
        }
        check("{\"a\":1}", EventStream.readEvent(new java.io.ByteArrayInputStream(": comment\r\ndata:{\"a\":1}\r\n\r\n".getBytes(java.nio.charset.StandardCharsets.UTF_8))));
        check("one\ntwo", EventStream.readEvent(new java.io.ByteArrayInputStream("data: one\ndata: two\n\n".getBytes(java.nio.charset.StandardCharsets.UTF_8))));
        for (String incomplete : new String[] {"", "data: {}", "data: {}\n", "data: " + "a".repeat(128 * 1024) + "\n\n"}) {
            try { EventStream.readEvent(new java.io.ByteArrayInputStream(incomplete.getBytes(java.nio.charset.StandardCharsets.UTF_8))); throw new AssertionError("Accepted incomplete/oversized event"); }
            catch (java.io.IOException expected) { }
        }
        if (EventStream.retryDelay(1) != 1000 || EventStream.retryDelay(100) != 15000) throw new AssertionError("Retry bounds");
        System.out.println("PASS: Android power policy, LAN addresses, event framing and retry bounds (27 checks)");
    }
}
