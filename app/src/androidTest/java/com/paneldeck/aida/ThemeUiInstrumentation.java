package com.paneldeck.aida;

import android.app.Activity;
import android.app.Dialog;
import android.app.Instrumentation;
import android.content.Intent;
import android.content.pm.ActivityInfo;
import android.graphics.Bitmap;
import android.os.Bundle;
import android.os.SystemClock;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.Spinner;
import android.widget.TextView;

import java.io.File;
import java.io.FileOutputStream;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.util.concurrent.atomic.AtomicReference;

/** Test APK only: exercises this application's views without injecting global input. */
public final class ThemeUiInstrumentation extends Instrumentation {
    private MainActivity activity;
    private int checks;
    private int originalOrientation;
    private String originalTheme;
    private File output;

    @Override public void onCreate(Bundle arguments) { super.onCreate(arguments); start(); }

    @Override public void onStart() {
        Bundle result = new Bundle();
        int resultCode = Activity.RESULT_CANCELED;
        try {
            // HyperOS may block activity launches from the test application's
            // background UID. The shell can open the app's existing standard
            // settings Intent; this grants no permissions and adds no app entry.
            ActivityMonitor monitor = addMonitor(MainActivity.class.getName(), null, false);
            try (android.os.ParcelFileDescriptor command = getUiAutomation().executeShellCommand(
                    "am start -W -a android.intent.action.APPLICATION_PREFERENCES -n com.paneldeck.aida/.MainActivity");
                 android.os.ParcelFileDescriptor.AutoCloseInputStream stream = new android.os.ParcelFileDescriptor.AutoCloseInputStream(command)) {
                byte[] buffer = new byte[1024];
                while (stream.read(buffer) != -1) { }
            }
            activity = (MainActivity) waitForMonitorWithTimeout(monitor, 10000);
            removeMonitor(monitor);
            require(activity != null, "Activity launch completed");
            originalOrientation = onMain(() -> activity.getRequestedOrientation());
            originalTheme = onMain(() -> ScheduleManager.prefs(activity).getString(PanelTheme.KEY, "classic"));
            output = new File(activity.getExternalFilesDir(null), "theme-ui-tests");
            if (!output.exists() && !output.mkdirs()) throw new AssertionError("Cannot create test output");
            await(() -> onMain(() -> dialog() != null && dialog().isShowing()), 8000, "Settings opens");
            await(() -> onMain(() -> (boolean) field("desktopPageReady")), 8000, "Local panel ready");
            Spinner picker = onMain(() -> find(dialog().getWindow().getDecorView(), Spinner.class, null));
            require(picker != null, "Theme picker exists");
            require(picker.getCount() == PanelTheme.IDS.length, "All themes present");
            measureRows(picker);

            for (int index = 0; index < PanelTheme.IDS.length; index++) {
                final int position = index;
                onMain(() -> { picker.setSelection(position); return null; });
                await(() -> onMain(() -> PanelTheme.IDS[position].equals(field("activeTheme"))), 2000, "Theme selection applies");
                waitForIdleSync();
                SystemClock.sleep(80);
                onMain(() -> {
                    TextView selected = (TextView) picker.getSelectedView();
                    checkText(selected, "Selected theme " + position);
                    TextView description = find(dialog().getWindow().getDecorView(), TextView.class, PanelTheme.DESCRIPTIONS[position]);
                    require(description != null, "Selected description exists");
                    checkText(description, "Selected description " + position);
                    Button preview = find(dialog().getWindow().getDecorView(), Button.class, "全屏预览 4 秒");
                    require(preview != null && preview.getHeight() >= dp(48), "Preview has usable height");
                    preview.performClick();
                    return null;
                });
                await(() -> onMain(() -> !dialog().isShowing() && (boolean) field("themePreview")), 1000, "Preview hides settings");
                SystemClock.sleep(250);
                screenshot("preview-" + PanelTheme.IDS[position] + ".png");
                await(() -> onMain(() -> dialog().isShowing() && !(boolean) field("themePreview")), 5500, "Preview restores settings");
                require(onMain(() -> originalTheme.equals(ScheduleManager.prefs(activity).getString(PanelTheme.KEY, "classic"))), "Preview does not save preference");
            }
            SystemClock.sleep(1000);
            screenshot("settings-portrait.png");
            onMain(() -> { activity.setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE); return null; });
            await(() -> onMain(() -> activity.getResources().getDisplayMetrics().widthPixels > activity.getResources().getDisplayMetrics().heightPixels), 5000, "Landscape configuration");
            SystemClock.sleep(1300);
            onMain(() -> {
                View root = dialog().getWindow().getDecorView();
                require(root.getWidth() <= activity.getResources().getDisplayMetrics().widthPixels, "Landscape dialog fits width");
                require(root.getHeight() <= activity.getResources().getDisplayMetrics().heightPixels, "Landscape dialog fits height");
                require(root.findViewWithTag("control-eyebrow").getVisibility() == View.GONE, "Short screen header is compact");
                return null;
            });
            screenshot("settings-landscape.png");
            onMain(() -> { dialog().dismiss(); return null; });
            require(onMain(() -> originalTheme.equals(field("activeTheme"))), "Cancel restores saved theme");
            onMain(() -> { activity.setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_PORTRAIT); invoke("showControlPanel"); return null; });
            await(() -> onMain(() -> activity.getResources().getDisplayMetrics().heightPixels > activity.getResources().getDisplayMetrics().widthPixels), 5000, "Portrait restored");
            SystemClock.sleep(1300);
            onMain(() -> { Spinner reopened = find(dialog().getWindow().getDecorView(), Spinner.class, null); reopened.performClick(); return null; });
            SystemClock.sleep(800);
            screenshot("theme-choices.png");
            result.putString("stream", "PASS " + checks + " checks. Screenshots: " + output.getAbsolutePath() + "\n");
            result.putString("screenshots", output.getAbsolutePath());
            result.putInt("checks", checks);
            resultCode = Activity.RESULT_OK;
        } catch (Throwable error) {
            result.putString("stream", "FAIL " + error + "\n" + android.util.Log.getStackTraceString(error));
        } finally {
            if (activity != null) runOnMainSync(() -> {
                try { if (dialog() != null) dialog().dismiss(); }
                catch (Exception ignored) { }
                activity.setRequestedOrientation(originalOrientation);
            });
        }
        finish(resultCode, result);
    }

    private void measureRows(Spinner picker) throws Exception {
        onMain(() -> {
            for (int width : new int[] { 220, 280, 520 }) for (float textScale : new float[] { 1f, 1.3f }) {
                for (int position = 0; position < PanelTheme.IDS.length; position++) {
                    TextView row = (TextView) picker.getAdapter().getDropDownView(position, null, new FrameLayout(activity));
                    row.setTextSize(android.util.TypedValue.COMPLEX_UNIT_PX, row.getTextSize() * textScale);
                    row.measure(View.MeasureSpec.makeMeasureSpec(dp(width), View.MeasureSpec.EXACTLY), View.MeasureSpec.makeMeasureSpec(0, View.MeasureSpec.UNSPECIFIED));
                    row.layout(0, 0, dp(width), row.getMeasuredHeight());
                    checkText(row, "Option " + position + " width " + width + " scale " + textScale);
                    require(row.getText().toString().contains(PanelTheme.DESCRIPTIONS[position]), "Choice contains description");
                }
            }
            return null;
        });
    }

    private void checkText(TextView view, String label) {
        require(view.getLayout() != null, label + " has layout");
        int lines = view.getLayout().getLineCount();
        require(lines > 0 && view.getLayout().getLineBottom(lines - 1) <= view.getHeight() - view.getCompoundPaddingTop() - view.getCompoundPaddingBottom(), label + " has no vertical clipping");
        for (int i = 0; i < lines; i++) require(view.getLayout().getEllipsisCount(i) == 0, label + " has no ellipsis");
    }

    private void screenshot(String name) throws Exception {
        Bitmap bitmap = getUiAutomation().takeScreenshot();
        require(bitmap != null, "Screenshot available");
        try (FileOutputStream file = new FileOutputStream(new File(output, name))) { bitmap.compress(Bitmap.CompressFormat.PNG, 100, file); }
        finally { bitmap.recycle(); }
    }

    private int dp(int value) { return Math.round(value * activity.getResources().getDisplayMetrics().density); }
    private void require(boolean condition, String label) { if (!condition) throw new AssertionError(label); checks++; }
    private Dialog dialog() throws Exception { return (Dialog) field("controlDialog"); }
    private Object field(String name) throws Exception { Field field = MainActivity.class.getDeclaredField(name); field.setAccessible(true); return field.get(activity); }
    private void invoke(String name) throws Exception { Method method = MainActivity.class.getDeclaredMethod(name); method.setAccessible(true); method.invoke(activity); }
    private <T> T onMain(CheckedSupplier<T> operation) throws Exception {
        AtomicReference<T> value = new AtomicReference<>();
        AtomicReference<Throwable> error = new AtomicReference<>();
        runOnMainSync(() -> { try { value.set(operation.get()); } catch (Throwable e) { error.set(e); } });
        if (error.get() != null) throw new Exception(error.get());
        return value.get();
    }
    private void await(CheckedSupplier<Boolean> condition, long timeout, String label) throws Exception {
        long end = SystemClock.uptimeMillis() + timeout;
        while (SystemClock.uptimeMillis() < end) { if (condition.get()) { checks++; return; } SystemClock.sleep(60); }
        throw new AssertionError("Timed out: " + label);
    }
    private <T extends View> T find(View view, Class<T> type, String text) {
        if (type.isInstance(view) && (text == null || view instanceof TextView && text.contentEquals(((TextView) view).getText()))) return type.cast(view);
        if (view instanceof ViewGroup) for (int i = 0; i < ((ViewGroup) view).getChildCount(); i++) {
            T found = find(((ViewGroup) view).getChildAt(i), type, text);
            if (found != null) return found;
        }
        return null;
    }
    private interface CheckedSupplier<T> { T get() throws Exception; }
}
