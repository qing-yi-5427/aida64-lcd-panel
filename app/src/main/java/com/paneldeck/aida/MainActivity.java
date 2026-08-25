package com.paneldeck.aida;

import android.app.Activity;
import android.app.Dialog;
import android.app.KeyguardManager;
import android.app.TimePickerDialog;
import android.annotation.SuppressLint;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.provider.Settings;
import android.text.InputType;
import android.view.GestureDetector;
import android.view.Gravity;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.Window;
import android.view.WindowInsets;
import android.view.WindowInsetsController;
import android.view.WindowManager;
import android.webkit.CookieManager;
import android.webkit.SslErrorHandler;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Button;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.ScrollView;
import android.widget.SeekBar;
import android.widget.Space;
import android.widget.Switch;
import android.widget.TextView;
import android.widget.Toast;

import java.util.Locale;

public final class MainActivity extends Activity {
    private static final int INK = Color.rgb(8, 10, 11);
    private static final int SIGNAL = Color.rgb(200, 255, 51);
    private static final String KEY_CONFIGURED = "initial_setup_complete";
    static final String ACTION_WAKE = "com.paneldeck.aida.WAKE_PANEL";

    private SharedPreferences prefs;
    private WebView panel;
    private ProgressBar progress;
    private TextView settingsButton;
    private TextView holidaySyncStatus;
    private FrameLayout sleepOverlay;
    private GestureDetector gestures;
    private boolean receiverRegistered;

    private final android.os.Handler handler = new android.os.Handler(android.os.Looper.getMainLooper());
    private final Runnable hideSettingsButton = () -> {
        if (settingsButton != null && settingsButton.getVisibility() == View.VISIBLE) {
            settingsButton.animate().alpha(0f).scaleX(.7f).scaleY(.7f).setDuration(180)
                    .withEndAction(() -> settingsButton.setVisibility(View.GONE)).start();
        }
    };
    private final Runnable stateTicker = new Runnable() {
        @Override public void run() {
            applyScreenMode();
            handler.postDelayed(this, 30_000L);
        }
    };
    private final BroadcastReceiver modeReceiver = new BroadcastReceiver() {
        @Override public void onReceive(Context context, Intent intent) { applyScreenMode(); }
    };

    @Override protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        ScheduleManager.ensureDefaults(this);
        HolidaySync.ensureOfflineData(this);
        prefs = ScheduleManager.prefs(this);
        prepareWakeWindow();
        buildInterface();
        enterImmersiveMode();
        if (savedInstanceState != null) panel.restoreState(savedInstanceState);
        else panel.loadUrl(normalizeUrl(prefs.getString(ScheduleManager.KEY_HOME_URL, ScheduleManager.DEFAULT_HOME)));
        if (Intent.ACTION_APPLICATION_PREFERENCES.equals(getIntent().getAction())) {
            handler.postDelayed(this::showControlPanel, 300L);
        } else if (ACTION_WAKE.equals(getIntent().getAction())) {
            handler.postDelayed(this::finishScheduledWake, 120L);
        } else if (!prefs.getBoolean(KEY_CONFIGURED, false)) {
            prefs.edit().putLong(ScheduleManager.KEY_OVERRIDE_UNTIL, System.currentTimeMillis() + 10 * 60_000L).apply();
            handler.postDelayed(this::showControlPanel, 450L);
        }
        ScheduleManager.scheduleNext(this);
        HolidaySync.syncAsync(this, false, updated -> {
            if (updated) applyScreenMode();
            if (holidaySyncStatus != null) holidaySyncStatus.setText(HolidaySync.status(this));
        });
    }

    @Override protected void onStart() {
        super.onStart();
        if (!receiverRegistered) {
            IntentFilter filter = new IntentFilter(ScheduleReceiver.ACTION_APPLY);
            if (Build.VERSION.SDK_INT >= 33) registerReceiver(modeReceiver, filter, Context.RECEIVER_NOT_EXPORTED);
            else registerLegacyReceiver(filter);
            receiverRegistered = true;
        }
    }

    @SuppressLint("UnspecifiedRegisterReceiverFlag")
    private void registerLegacyReceiver(IntentFilter filter) {
        registerReceiver(modeReceiver, filter);
    }

    @Override protected void onResume() {
        super.onResume();
        enterImmersiveMode();
        panel.onResume();
        handler.removeCallbacks(stateTicker);
        handler.post(stateTicker);
    }

    @Override protected void onPause() {
        handler.removeCallbacks(stateTicker);
        panel.onPause();
        super.onPause();
    }

    @Override protected void onStop() {
        if (receiverRegistered) {
            unregisterReceiver(modeReceiver);
            receiverRegistered = false;
        }
        super.onStop();
    }

    @Override protected void onSaveInstanceState(Bundle outState) {
        panel.saveState(outState);
        super.onSaveInstanceState(outState);
    }

    @Override protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        applyScreenMode();
        if (Intent.ACTION_APPLICATION_PREFERENCES.equals(intent.getAction())) {
            handler.postDelayed(this::showControlPanel, 180L);
        } else if (ACTION_WAKE.equals(intent.getAction())) {
            prepareWakeWindow();
            handler.postDelayed(this::finishScheduledWake, 80L);
        }
    }

    @Override public void onBackPressed() { showSettingsButton(); }

    private void enterImmersiveMode() {
        getWindow().setStatusBarColor(Color.TRANSPARENT);
        getWindow().setNavigationBarColor(Color.TRANSPARENT);
        if (Build.VERSION.SDK_INT >= 30) {
            getWindow().setDecorFitsSystemWindows(false);
            WindowInsetsController controller = getWindow().getDecorView().getWindowInsetsController();
            if (controller != null) {
                controller.hide(WindowInsets.Type.statusBars() | WindowInsets.Type.navigationBars());
                controller.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
            }
        } else {
            getWindow().getDecorView().setSystemUiVisibility(
                    View.SYSTEM_UI_FLAG_FULLSCREEN | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                            | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                            | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION | View.SYSTEM_UI_FLAG_LAYOUT_STABLE);
        }
    }

    private void buildInterface() {
        FrameLayout root = new FrameLayout(this);
        root.setBackgroundColor(Color.BLACK);
        panel = new WebView(this);
        configurePanel();
        root.addView(panel, match());

        progress = new ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal);
        progress.setMax(100);
        progress.setProgressTintList(android.content.res.ColorStateList.valueOf(SIGNAL));
        progress.setProgressBackgroundTintList(android.content.res.ColorStateList.valueOf(Color.TRANSPARENT));
        root.addView(progress, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(2), Gravity.TOP));

        settingsButton = new TextView(this);
        settingsButton.setText("⚙");
        settingsButton.setTextColor(INK);
        settingsButton.setTextSize(24);
        settingsButton.setGravity(Gravity.CENTER);
        settingsButton.setContentDescription("面板设置");
        settingsButton.setElevation(dp(12));
        settingsButton.setBackground(circle(SIGNAL));
        settingsButton.setVisibility(View.GONE);
        settingsButton.setOnClickListener(v -> showControlPanel());
        FrameLayout.LayoutParams buttonLayout = new FrameLayout.LayoutParams(dp(54), dp(54), Gravity.END | Gravity.BOTTOM);
        buttonLayout.setMargins(0, 0, dp(20), dp(24));
        root.addView(settingsButton, buttonLayout);

        sleepOverlay = buildSleepOverlay();
        sleepOverlay.setVisibility(View.GONE);
        root.addView(sleepOverlay, match());

        gestures = new GestureDetector(this, new GestureDetector.SimpleOnGestureListener() {
            @Override public boolean onDown(MotionEvent e) { return true; }
            @Override public boolean onFling(MotionEvent first, MotionEvent last, float velocityX, float velocityY) {
                if (first == null || last == null) return false;
                float dx = last.getX() - first.getX();
                float dy = last.getY() - first.getY();
                if (Math.abs(dy) > Math.abs(dx) && Math.abs(dy) > dp(55)) {
                    if (dy < 0 && velocityY < -250) showSettingsButton();
                    else if (dy > 0 && velocityY > 250) hideSettingsButtonNow();
                }
                return false;
            }
        });
        View.OnTouchListener gestureListener = (v, event) -> {
            gestures.onTouchEvent(event);
            return false;
        };
        panel.setOnTouchListener(gestureListener);
        sleepOverlay.setOnTouchListener(gestureListener);
        setContentView(root);
    }

    private void configurePanel() {
        WebSettings settings = panel.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setDatabaseEnabled(true);
        settings.setBuiltInZoomControls(true);
        settings.setDisplayZoomControls(false);
        settings.setSupportZoom(true);
        settings.setUseWideViewPort(true);
        settings.setLoadWithOverviewMode(true);
        settings.setMediaPlaybackRequiresUserGesture(false);
        settings.setMixedContentMode(WebSettings.MIXED_CONTENT_COMPATIBILITY_MODE);
        settings.setAllowFileAccess(false);
        CookieManager.getInstance().setAcceptCookie(true);
        panel.setBackgroundColor(Color.BLACK);
        panel.setOverScrollMode(View.OVER_SCROLL_NEVER);
        panel.setWebViewClient(new WebViewClient() {
            @Override public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                String scheme = request.getUrl().getScheme();
                return !("http".equalsIgnoreCase(scheme) || "https".equalsIgnoreCase(scheme));
            }
            @Override public void onReceivedSslError(WebView view, SslErrorHandler handler, android.net.http.SslError error) {
                handler.cancel();
                Toast.makeText(MainActivity.this, "面板证书无效，已停止加载", Toast.LENGTH_LONG).show();
            }
            @Override public void onPageFinished(WebView view, String url) {
                // Some sensor pages disable user scaling in their viewport. Re-enable it for panel inspection.
                view.evaluateJavascript("(function(){var m=document.querySelector('meta[name=viewport]');"
                        + "if(!m){m=document.createElement('meta');m.name='viewport';document.head.appendChild(m);}"
                        + "var c=m.content||'width=device-width';"
                        + "c=c.replace(/user-scalable\\s*=\\s*[^,]+/ig,'').replace(/maximum-scale\\s*=\\s*[^,]+/ig,'');"
                        + "m.content=c+',maximum-scale=5,user-scalable=yes';})();", null);
            }
        });
        panel.setWebChromeClient(new WebChromeClient() {
            @Override public void onProgressChanged(WebView view, int value) {
                progress.setProgress(value);
                progress.setVisibility(value >= 100 ? View.GONE : View.VISIBLE);
            }
        });
    }

    private FrameLayout buildSleepOverlay() {
        FrameLayout overlay = new FrameLayout(this);
        overlay.setBackgroundColor(Color.BLACK);
        overlay.setClickable(true);
        overlay.setLongClickable(true);
        TextView hint = new TextView(this);
        hint.setText("长按临时唤醒");
        hint.setTextColor(Color.rgb(14, 14, 14));
        hint.setTextSize(12);
        hint.setGravity(Gravity.CENTER);
        overlay.addView(hint, match());
        overlay.setOnLongClickListener(v -> {
            prefs.edit().putLong(ScheduleManager.KEY_OVERRIDE_UNTIL, System.currentTimeMillis() + 10 * 60_000L).apply();
            ScheduleManager.scheduleNext(this);
            applyScreenMode();
            Toast.makeText(this, "临时唤醒 10 分钟", Toast.LENGTH_SHORT).show();
            return true;
        });
        return overlay;
    }

    private void showSettingsButton() {
        handler.removeCallbacks(hideSettingsButton);
        settingsButton.animate().cancel();
        settingsButton.setVisibility(View.VISIBLE);
        settingsButton.setAlpha(0f);
        settingsButton.setScaleX(.72f);
        settingsButton.setScaleY(.72f);
        settingsButton.animate().alpha(1f).scaleX(1f).scaleY(1f).setDuration(180).start();
        handler.postDelayed(hideSettingsButton, 5_000L);
    }

    private void hideSettingsButtonNow() {
        handler.removeCallbacks(hideSettingsButton);
        if (settingsButton.getVisibility() != View.VISIBLE) return;
        settingsButton.animate().cancel();
        settingsButton.animate().alpha(0f).translationY(dp(14)).scaleX(.72f).scaleY(.72f)
                .setDuration(160).withEndAction(() -> {
                    settingsButton.setVisibility(View.GONE);
                    settingsButton.setTranslationY(0f);
                }).start();
    }

    private void showControlPanel() {
        handler.removeCallbacks(hideSettingsButton);
        settingsButton.setVisibility(View.GONE);
        SharedPreferences p = prefs;

        Dialog dialog = new Dialog(this);
        dialog.requestWindowFeature(Window.FEATURE_NO_TITLE);

        LinearLayout shell = new LinearLayout(this);
        shell.setOrientation(LinearLayout.VERTICAL);
        shell.setBackground(rounded(Color.rgb(242, 241, 235), 24));

        LinearLayout header = new LinearLayout(this);
        header.setGravity(Gravity.CENTER_VERTICAL);
        header.setPadding(dp(22), dp(17), dp(14), dp(17));
        header.setBackground(topRounded(INK, 24));
        LinearLayout heading = new LinearLayout(this);
        heading.setOrientation(LinearLayout.VERTICAL);
        TextView eyebrow = text("曜屏  /  PANEL CONTROL", 10, SIGNAL);
        eyebrow.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
        eyebrow.setLetterSpacing(.12f);
        TextView title = text("面板设置", 23, Color.WHITE);
        title.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        title.setPadding(0, dp(3), 0, 0);
        heading.addView(eyebrow, matchWrap());
        heading.addView(title, matchWrap());
        header.addView(heading, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        TextView close = text("×", 28, Color.rgb(190, 195, 190));
        close.setGravity(Gravity.CENTER);
        close.setContentDescription("关闭设置");
        close.setBackground(circle(Color.rgb(29, 34, 35)));
        close.setOnClickListener(v -> dialog.dismiss());
        header.addView(close, new LinearLayout.LayoutParams(dp(42), dp(42)));
        shell.addView(header, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        LinearLayout form = new LinearLayout(this);
        form.setOrientation(LinearLayout.VERTICAL);
        form.setPadding(dp(18), dp(16), dp(18), dp(16));

        LinearLayout stateCard = card(INK);
        TextView stateDot = text(ScheduleManager.isOff(this, System.currentTimeMillis()) ? "●  息屏计划" : "●  面板常亮", 12, SIGNAL);
        stateDot.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
        TextView state = text(scheduleSummary(), 12, Color.rgb(184, 190, 185));
        state.setPadding(0, dp(4), 0, 0);
        stateCard.addView(stateDot, matchWrap());
        stateCard.addView(state, matchWrap());
        form.addView(stateCard, cardParams());

        form.addView(section("面板连接"), matchWrap());
        EditText home = input("AIDA64 面板地址", p.getString(ScheduleManager.KEY_HOME_URL, ScheduleManager.DEFAULT_HOME));
        form.addView(home, matchWrap());

        LinearLayout powerCard = card(Color.WHITE);
        Switch enabled = new Switch(this);
        enabled.setText("自动控制屏幕常亮");
        enabled.setTextColor(INK);
        enabled.setTextSize(15);
        enabled.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        enabled.setChecked(p.getBoolean(ScheduleManager.KEY_ENABLED, true));
        enabled.setPadding(0, 0, 0, 0);
        int[][] switchStates = new int[][] { new int[] { android.R.attr.state_checked }, new int[] {} };
        enabled.setThumbTintList(new android.content.res.ColorStateList(switchStates, new int[] { SIGNAL, Color.rgb(170, 174, 170) }));
        enabled.setTrackTintList(new android.content.res.ColorStateList(switchStates, new int[] { Color.rgb(88, 105, 45), Color.rgb(215, 216, 211) }));
        powerCard.addView(enabled, matchWrap());
        form.addView(powerCard, cardParams());

        form.addView(section("亮屏时段"), matchWrap());
        LinearLayout scheduleCard = card(Color.WHITE);
        TextView workTitle = text("工作日", 13, INK);
        workTitle.setTypeface(Typeface.DEFAULT_BOLD);
        scheduleCard.addView(workTitle, matchWrap());
        int[] workStart = {p.getInt(ScheduleManager.KEY_WORK_START, 120)};
        int[] workEnd = {p.getInt(ScheduleManager.KEY_WORK_END, 1230)};
        scheduleCard.addView(timeRow("开始", workStart, "结束", workEnd), matchWrap());
        scheduleCard.addView(divider(), new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(1)));
        TextView restTitle = text("周末 / 节假日", 13, INK);
        restTitle.setTypeface(Typeface.DEFAULT_BOLD);
        restTitle.setPadding(0, dp(12), 0, 0);
        scheduleCard.addView(restTitle, matchWrap());
        int[] restStart = {p.getInt(ScheduleManager.KEY_REST_START, 120)};
        int[] restEnd = {p.getInt(ScheduleManager.KEY_REST_END, 480)};
        scheduleCard.addView(timeRow("开始", restStart, "结束", restEnd), matchWrap());
        form.addView(scheduleCard, cardParams());

        form.addView(section("中国大陆节假日"), matchWrap());
        LinearLayout holidayCard = card(Color.WHITE);
        TextView holidayTitle = text("法定节假日与调休", 14, INK);
        holidayTitle.setTypeface(Typeface.DEFAULT_BOLD);
        TextView holidayStatus = text(HolidaySync.status(this), 11, Color.rgb(99, 105, 101));
        holidaySyncStatus = holidayStatus;
        holidayStatus.setPadding(0, dp(5), 0, 0);
        holidayCard.addView(holidayTitle, matchWrap());
        holidayCard.addView(holidayStatus, matchWrap());
        form.addView(holidayCard, cardParams());

        int initialBrightness = Math.round(p.getFloat(ScheduleManager.KEY_BRIGHTNESS, .85f) * 100);
        TextView brightnessLabel = section(String.format(Locale.CHINA, "常亮亮度 · %d%%", initialBrightness));
        form.addView(brightnessLabel, matchWrap());
        LinearLayout brightnessCard = card(Color.WHITE);
        SeekBar brightness = new SeekBar(this);
        brightness.setMax(100);
        brightness.setProgress(initialBrightness);
        brightness.setProgressTintList(android.content.res.ColorStateList.valueOf(SIGNAL));
        brightness.setThumbTintList(android.content.res.ColorStateList.valueOf(INK));
        brightness.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener() {
            @Override public void onProgressChanged(SeekBar bar, int value, boolean user) {
                brightnessLabel.setText(String.format(Locale.CHINA, "常亮亮度 · %d%%", Math.max(5, value)));
            }
            @Override public void onStartTrackingTouch(SeekBar bar) {}
            @Override public void onStopTrackingTouch(SeekBar bar) {}
        });
        brightnessCard.addView(brightness, matchWrap());
        form.addView(brightnessCard, cardParams());

        LinearLayout actions = new LinearLayout(this);
        Button reload = smallButton("重新加载面板");
        styleOutlineButton(reload);
        reload.setOnClickListener(v -> { panel.reload(); Toast.makeText(this, "正在重新加载", Toast.LENGTH_SHORT).show(); });
        Button exact = smallButton(ScheduleManager.canScheduleExactly(this) ? "精确定时已开启" : "开启精确定时");
        styleOutlineButton(exact);
        exact.setEnabled(!ScheduleManager.canScheduleExactly(this));
        exact.setOnClickListener(v -> requestExactAlarm());
        actions.addView(reload, new LinearLayout.LayoutParams(0, dp(48), 1f));
        actions.addView(new Space(this), new LinearLayout.LayoutParams(dp(8), 1));
        actions.addView(exact, new LinearLayout.LayoutParams(0, dp(48), 1f));
        form.addView(actions, matchWrap());

        ScrollView scroll = new ScrollView(this);
        scroll.setFillViewport(true);
        scroll.setVerticalScrollBarEnabled(false);
        scroll.addView(form);
        shell.addView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f));

        LinearLayout footer = new LinearLayout(this);
        footer.setGravity(Gravity.END | Gravity.CENTER_VERTICAL);
        footer.setPadding(dp(18), dp(12), dp(18), dp(16));
        footer.setBackground(bottomRounded(Color.rgb(242, 241, 235), 24));
        Button cancel = smallButton("取消");
        cancel.setTextColor(Color.rgb(70, 75, 72));
        cancel.setBackgroundColor(Color.TRANSPARENT);
        cancel.setOnClickListener(v -> dialog.dismiss());
        Button save = smallButton("保存设置");
        save.setTextColor(INK);
        save.setTypeface(Typeface.DEFAULT_BOLD);
        save.setBackground(rounded(SIGNAL, 13));
        footer.addView(cancel, new LinearLayout.LayoutParams(dp(78), dp(48)));
        footer.addView(save, new LinearLayout.LayoutParams(dp(116), dp(48)));
        shell.addView(footer, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        save.setOnClickListener(v -> {
            String url = home.getText().toString().trim();
            if (url.isEmpty()) { home.setError("请输入 AIDA64 面板地址"); return; }
            String normalized = normalizeUrl(url);
            String previous = p.getString(ScheduleManager.KEY_HOME_URL, "");
            p.edit().putBoolean(KEY_CONFIGURED, true)
                    .putBoolean(ScheduleManager.KEY_ENABLED, enabled.isChecked())
                    .putString(ScheduleManager.KEY_HOME_URL, normalized)
                    .putInt(ScheduleManager.KEY_WORK_START, workStart[0]).putInt(ScheduleManager.KEY_WORK_END, workEnd[0])
                    .putInt(ScheduleManager.KEY_REST_START, restStart[0]).putInt(ScheduleManager.KEY_REST_END, restEnd[0])
                    .putFloat(ScheduleManager.KEY_BRIGHTNESS, Math.max(5, brightness.getProgress()) / 100f)
                    .putLong(ScheduleManager.KEY_OVERRIDE_UNTIL, 0L).apply();
            if (!normalized.equals(previous)) panel.loadUrl(normalized);
            ScheduleManager.scheduleNext(this);
            applyScreenMode();
            dialog.dismiss();
        });

        dialog.setContentView(shell);
        dialog.setOnDismissListener(ignored -> {
            holidaySyncStatus = null;
            enterImmersiveMode();
        });
        dialog.show();
        Window window = dialog.getWindow();
        if (window != null) {
            window.setBackgroundDrawableResource(android.R.color.transparent);
            WindowManager.LayoutParams params = window.getAttributes();
            params.dimAmount = .72f;
            window.addFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);
            window.setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
            int screenWidth = getResources().getDisplayMetrics().widthPixels;
            int screenHeight = getResources().getDisplayMetrics().heightPixels;
            window.setLayout(screenWidth - dp(28), Math.min(screenHeight - dp(54), dp(760)));
        }
    }

    private void applyScreenMode() {
        if (sleepOverlay == null) return;
        boolean off = ScheduleManager.isOff(this, System.currentTimeMillis());
        WindowManager.LayoutParams params = getWindow().getAttributes();
        if (off) {
            getWindow().clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
            params.screenBrightness = 0f;
            sleepOverlay.setVisibility(View.VISIBLE);
        } else {
            params.screenBrightness = Math.max(.05f, prefs.getFloat(ScheduleManager.KEY_BRIGHTNESS, .85f));
            getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
            sleepOverlay.setVisibility(View.GONE);
        }
        getWindow().setAttributes(params);
    }

    @SuppressWarnings("deprecation")
    private void prepareWakeWindow() {
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED
                | WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON
                | WindowManager.LayoutParams.FLAG_DISMISS_KEYGUARD);
        if (Build.VERSION.SDK_INT >= 27) {
            setShowWhenLocked(true);
            setTurnScreenOn(true);
        }
    }

    private void finishScheduledWake() {
        prepareWakeWindow();
        applyScreenMode();
        KeyguardManager keyguard = (KeyguardManager) getSystemService(Context.KEYGUARD_SERVICE);
        if (Build.VERSION.SDK_INT >= 26 && keyguard != null && keyguard.isKeyguardLocked()) {
            keyguard.requestDismissKeyguard(this, null);
        }
        ScheduleManager.scheduleNext(this);
    }

    private String scheduleSummary() {
        long now = System.currentTimeMillis();
        boolean off = ScheduleManager.isOff(this, now);
        long next = ScheduleManager.nextChange(this, now);
        if (next == 0) return prefs.getBoolean(ScheduleManager.KEY_ENABLED, true) ? "计划已启用" : "计划已关闭，屏幕保持常亮";
        return (off ? "当前：息屏" : "当前：常亮") + "  ·  下次切换：" + ScheduleManager.formatNext(this, next);
    }

    private View timeRow(String firstLabel, int[] first, String secondLabel, int[] second) {
        LinearLayout row = new LinearLayout(this);
        Button firstButton = timeButton(firstLabel, first[0]);
        Button secondButton = timeButton(secondLabel, second[0]);
        firstButton.setOnClickListener(v -> pickTime(firstButton, firstLabel, first));
        secondButton.setOnClickListener(v -> pickTime(secondButton, secondLabel, second));
        row.addView(firstButton, new LinearLayout.LayoutParams(0, dp(66), 1f));
        row.addView(new Space(this), new LinearLayout.LayoutParams(dp(8), 1));
        row.addView(secondButton, new LinearLayout.LayoutParams(0, dp(66), 1f));
        return row;
    }

    private Button timeButton(String label, int minute) {
        Button button = smallButton(String.format(Locale.CHINA, "%s\n%s", label, ScheduleManager.formatTime(minute)));
        button.setTextColor(Color.WHITE);
        button.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
        button.setBackground(rounded(Color.rgb(26, 30, 31), 12));
        return button;
    }

    private void pickTime(Button button, String label, int[] value) {
        new TimePickerDialog(this, (view, hour, minute) -> {
            value[0] = hour * 60 + minute;
            button.setText(String.format(Locale.CHINA, "%s\n%s", label, ScheduleManager.formatTime(value[0])));
        }, value[0] / 60, value[0] % 60, true).show();
    }

    private void requestExactAlarm() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            try { startActivity(new Intent(Settings.ACTION_REQUEST_SCHEDULE_EXACT_ALARM, Uri.parse("package:" + getPackageName()))); }
            catch (Exception e) { startActivity(new Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.parse("package:" + getPackageName()))); }
        }
    }

    private String normalizeUrl(String raw) {
        String value = raw == null ? "" : raw.trim();
        if (value.matches("^[a-zA-Z][a-zA-Z0-9+.-]*://.*")) return value;
        return "http://" + value;
    }

    private EditText input(String hint, String value) {
        EditText field = new EditText(this);
        field.setHint(hint);
        field.setHintTextColor(Color.rgb(130, 134, 130));
        field.setText(value);
        field.setTextColor(INK);
        field.setSingleLine(true);
        field.setTextSize(14);
        field.setSelectAllOnFocus(true);
        field.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_URI);
        field.setPadding(dp(14), 0, dp(14), 0);
        field.setBackground(roundedStroke(Color.WHITE, 12, Color.rgb(204, 207, 200), 1));
        field.setMinHeight(dp(52));
        return field;
    }

    private Button smallButton(String value) {
        Button button = new Button(this);
        button.setText(value);
        button.setTextSize(13);
        button.setAllCaps(false);
        return button;
    }

    private TextView section(String value) {
        TextView label = text(value, 11, Color.rgb(73, 80, 77));
        label.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
        label.setLetterSpacing(.08f);
        label.setPadding(dp(2), dp(12), 0, dp(7));
        return label;
    }

    private TextView text(String value, float size, int color) {
        TextView label = new TextView(this);
        label.setText(value);
        label.setTextSize(size);
        label.setTextColor(color);
        return label;
    }

    private GradientDrawable circle(int color) {
        GradientDrawable drawable = new GradientDrawable();
        drawable.setShape(GradientDrawable.OVAL);
        drawable.setColor(color);
        return drawable;
    }

    private GradientDrawable rounded(int color, int radiusDp) {
        GradientDrawable drawable = new GradientDrawable();
        drawable.setColor(color);
        drawable.setCornerRadius(dp(radiusDp));
        return drawable;
    }

    private GradientDrawable roundedStroke(int color, int radiusDp, int strokeColor, int strokeDp) {
        GradientDrawable drawable = rounded(color, radiusDp);
        drawable.setStroke(dp(strokeDp), strokeColor);
        return drawable;
    }

    private GradientDrawable topRounded(int color, int radiusDp) {
        GradientDrawable drawable = new GradientDrawable();
        drawable.setColor(color);
        float radius = dp(radiusDp);
        drawable.setCornerRadii(new float[] { radius, radius, radius, radius, 0, 0, 0, 0 });
        return drawable;
    }

    private GradientDrawable bottomRounded(int color, int radiusDp) {
        GradientDrawable drawable = new GradientDrawable();
        drawable.setColor(color);
        float radius = dp(radiusDp);
        drawable.setCornerRadii(new float[] { 0, 0, 0, 0, radius, radius, radius, radius });
        return drawable;
    }

    private LinearLayout card(int color) {
        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setPadding(dp(14), dp(13), dp(14), dp(13));
        card.setBackground(rounded(color, 15));
        return card;
    }

    private LinearLayout.LayoutParams cardParams() {
        LinearLayout.LayoutParams params = matchWrap();
        params.setMargins(0, 0, 0, dp(3));
        return params;
    }

    private View divider() {
        View divider = new View(this);
        divider.setBackgroundColor(Color.rgb(229, 230, 225));
        return divider;
    }

    private void styleOutlineButton(Button button) {
        button.setTextColor(INK);
        button.setBackground(roundedStroke(Color.TRANSPARENT, 12, Color.rgb(176, 180, 173), 1));
    }

    private int dp(int value) { return Math.round(value * getResources().getDisplayMetrics().density); }
    private FrameLayout.LayoutParams match() { return new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT); }
    private LinearLayout.LayoutParams matchWrap() { return new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT); }
}
