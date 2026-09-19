package com.paneldeck.aida;

import android.app.*;
import android.content.*;
import android.os.*;
import android.view.*;
import android.widget.*;
import org.json.JSONObject;
import java.util.*;
import java.util.concurrent.atomic.AtomicReference;

/** Uses only an isolated loopback fixture. Restores every production preference. */
public final class ThemeSyncInstrumentation extends Instrumentation {
    private MainActivity activity;
    private int checks;
    private final String endpoint = "http://127.0.0.1:19864", token = new String(new char[64]).replace('\0', 'a');
    @Override public void onCreate(Bundle arguments) { super.onCreate(arguments); start(); }
    private Object field(String name) throws Exception { java.lang.reflect.Field f=MainActivity.class.getDeclaredField(name);f.setAccessible(true);return f.get(activity); }
    private Dialog dialog() throws Exception { return (Dialog)field("controlDialog"); }
    private void openSettings() throws Exception { java.lang.reflect.Method m=MainActivity.class.getDeclaredMethod("showControlPanel");m.setAccessible(true);m.invoke(activity); }
    private interface Work<T> { T run() throws Exception; }
    private <T> T ui(Work<T> work) throws Exception { AtomicReference<T> result=new AtomicReference<>();AtomicReference<Exception> error=new AtomicReference<>();runOnMainSync(()->{try{result.set(work.run());}catch(Exception e){error.set(e);}});if(error.get()!=null)throw error.get();return result.get(); }
    private void check(boolean value,String name) {if(!value)throw new AssertionError(name);checks++;}
    private void await(Work<Boolean> work,String name) throws Exception {long end=SystemClock.elapsedRealtime()+10000;while(SystemClock.elapsedRealtime()<end){if(work.run()){checks++;return;}SystemClock.sleep(100);}throw new AssertionError(name);}
    private <T extends View> T find(View v,Class<T> type,String text) {if(type.isInstance(v)&&(text==null||v instanceof TextView&&text.contentEquals(((TextView)v).getText())))return type.cast(v);if(v instanceof ViewGroup){ViewGroup g=(ViewGroup)v;for(int i=0;i<g.getChildCount();i++){T child=find(g.getChildAt(i),type,text);if(child!=null)return child;}}return null;}
    private String serverTheme(String theme,boolean initialize) throws Exception {return new JSONObject(DesktopLinkService.request(endpoint+"/api/phone/theme",token,new JSONObject().put("theme",theme).put("initialize",initialize).toString())).getString("theme");}
    private String readTheme() throws Exception {
        java.net.HttpURLConnection c=(java.net.HttpURLConnection)new java.net.URL(endpoint+"/api/phone/snapshot").openConnection();
        c.setConnectTimeout(2000);c.setReadTimeout(2000);c.setRequestProperty("Authorization","Bearer "+token);
        try(java.io.InputStream in=c.getInputStream();java.io.ByteArrayOutputStream out=new java.io.ByteArrayOutputStream()){
            byte[] bytes=new byte[4096];int n;while((n=in.read(bytes))!=-1){if(out.size()>131072)throw new java.io.IOException("Oversized fixture");out.write(bytes,0,n);}
            return new JSONObject(out.toString("UTF-8")).optString("panelTheme","");
        }finally{c.disconnect();}
    }
    @Override public void onStart() {
        Bundle result=new Bundle();int code=Activity.RESULT_CANCELED;
        Context context=getTargetContext();SharedPreferences prefs=ScheduleManager.prefs(context);Map<String,?> backup=new HashMap<>(prefs.getAll());
        try {
            context.stopService(new Intent(context,DesktopLinkService.class));
            prefs.edit().putBoolean(DesktopLinkService.KEY_ENABLED,true).putString(DesktopLinkService.KEY_URL,endpoint).putString(DesktopLinkService.KEY_TOKEN,token).putString(PanelTheme.KEY,"glass").remove(PanelTheme.PENDING).commit();
            ActivityMonitor monitor=addMonitor(MainActivity.class.getName(),null,false);
            shell("am start -W -a android.intent.action.APPLICATION_PREFERENCES -n com.paneldeck.aida/.MainActivity");
            activity=(MainActivity)waitForMonitorWithTimeout(monitor,10000);removeMonitor(monitor);check(activity!=null,"Activity started");
            await(()->DesktopLinkService.connected(),"Fixture connected");
            await(()->"on".equals(DesktopLinkService.screenMode()),"Fixture requests an awake display");
            await(()->ui(()->dialog()!=null),"Settings ready");
            await(()->ui(()->(boolean)field("desktopPageReady")),"Local panel assets ready");
            ui(()->{dialog().dismiss();return null;});
            await(()->"glass".equals(readTheme()),"First connection adopts phone theme, retrying optional failures");
            serverTheme("studio",false);
            await(()->"studio".equals(prefs.getString(PanelTheme.KEY,"")),"PC selection persisted on phone");
            await(()->ui(()->"studio".equals(field("activeTheme"))),"PC selection applied to WebView");
            ui(()->{openSettings();Spinner picker=find(dialog().getWindow().getDecorView(),Spinner.class,null);picker.setSelection(PanelTheme.indexOf("editorial"));return null;});
            await(()->ui(()->"editorial".equals(field("activeTheme"))),"Local draft preview applies");
            check("studio".equals(readTheme()),"Draft did not change PC");
            serverTheme("ambient",false);
            await(()->"ambient".equals(prefs.getString(PanelTheme.KEY,"")),"Remote change reaches saved state during draft");
            check(ui(()->"editorial".equals(field("activeTheme"))),"Remote change does not interrupt unsaved preview");
            ui(()->{dialog().dismiss();return null;});
            check(ui(()->"ambient".equals(field("activeTheme"))),"Cancel restores latest saved theme");
            ui(()->{openSettings();find(dialog().getWindow().getDecorView(),Spinner.class,null).setSelection(PanelTheme.indexOf("winui"));return null;});
            await(()->ui(()->"winui".equals(field("activeTheme"))),"Phone choice selected");
            ui(()->{Button save=find(dialog().getWindow().getDecorView(),Button.class,"保存设置");check(save!=null,"Save button exists");save.performClick();return null;});
            await(()->"winui".equals(readTheme()),"Phone save reaches PC");
            await(()->!prefs.contains(PanelTheme.PENDING),"Acknowledged phone change clears retry marker");
            serverTheme("glass",false);
            await(()->"glass".equals(prefs.getString(PanelTheme.KEY,"")),"Latest PC selection received");
            check("glass".equals(DesktopLinkService.saveTheme(prefs,"winui","winui")),"Unchanged old settings cannot overwrite remote choice");
            DesktopLinkService.saveTheme(prefs,"flutter","glass");
            check("flutter".equals(prefs.getString(PanelTheme.PENDING,"")),"Pending change is persisted before network delivery");
            DesktopLinkService.reconnect();
            await(()->"flutter".equals(readTheme()),"Reconnect sends pending selection");
            result.putString("stream","PASS "+checks+" true-device theme synchronization checks; synthetic fixture only.\n");code=Activity.RESULT_OK;
        } catch(Throwable error) {
            String detail="";
            try { detail=ui(()->" active="+field("activeTheme")+" ready="+field("desktopPageReady")+" dialog="+(dialog()!=null)+" mode="+DesktopLinkService.screenMode()); } catch(Exception ignored) {}
            result.putString("stream","FAIL "+detail+"\n"+android.util.Log.getStackTraceString(error));
        }
        finally {
            try {
                context.stopService(new Intent(context,DesktopLinkService.class));
                if(activity!=null)ui(()->{if(dialog()!=null)dialog().dismiss();activity.finish();return null;});
                SharedPreferences.Editor restore=prefs.edit().clear();
                for(Map.Entry<String,?> entry:backup.entrySet()){Object v=entry.getValue();String k=entry.getKey();if(v instanceof String)restore.putString(k,(String)v);else if(v instanceof Boolean)restore.putBoolean(k,(Boolean)v);else if(v instanceof Integer)restore.putInt(k,(Integer)v);else if(v instanceof Long)restore.putLong(k,(Long)v);else if(v instanceof Float)restore.putFloat(k,(Float)v);else if(v instanceof Set)restore.putStringSet(k,(Set<String>)v);}
                restore.commit();
            }catch(Throwable error){result.putString("stream",result.getString("stream","")+"RESTORE FAILURE "+error);code=Activity.RESULT_CANCELED;}
        }
        finish(code,result);
    }
    private void shell(String command) throws Exception {try(ParcelFileDescriptor fd=getUiAutomation().executeShellCommand(command);ParcelFileDescriptor.AutoCloseInputStream in=new ParcelFileDescriptor.AutoCloseInputStream(fd)){byte[] buffer=new byte[1024];while(in.read(buffer)!=-1){}}}
}
