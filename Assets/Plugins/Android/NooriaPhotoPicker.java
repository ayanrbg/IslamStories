package com.nooria.picker;

import android.app.Activity;
import android.app.Fragment;
import android.app.FragmentManager;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Matrix;
import android.media.ExifInterface;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.provider.MediaStore;

import com.unity3d.player.UnityPlayer;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;

/**
 * Picks one photo for the avatar without any storage permission: the system Photo Picker
 * on Android 13+, the system "open document" chooser before that (a content URI with a
 * one-off read grant). The picture is turned upright by its EXIF tag, scaled down to
 * 1600 px and written to the app cache; Unity gets the file path (or "" when cancelled)
 * through UnitySendMessage(gameObject, method, path).
 *
 * Called from C# (NativePhotoPicker.cs) by name: keep it in proguard-user.txt.
 */
public class NooriaPhotoPicker {
    private static final String TAG = "NooriaPhotoPicker";

    public static void pick(final String gameObject, final String method) {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) { UnityPlayer.UnitySendMessage(gameObject, method, ""); return; }
        activity.runOnUiThread(new Runnable() {
            @Override public void run() {
                PickerFragment fragment = new PickerFragment();
                Bundle args = new Bundle();
                args.putString("go", gameObject);
                args.putString("method", method);
                fragment.setArguments(args);
                FragmentManager fm = activity.getFragmentManager();
                fm.beginTransaction().add(fragment, TAG).commitAllowingStateLoss();
            }
        });
    }

    /** Headless fragment: the only way to get an activity result without our own Activity. */
    public static class PickerFragment extends Fragment {
        private static final int REQUEST = 7401;
        private boolean started;

        public PickerFragment() { }

        @Override public void onCreate(Bundle savedInstanceState) {
            super.onCreate(savedInstanceState);
            if (started) return;
            started = true;
            Intent intent;
            if (Build.VERSION.SDK_INT >= 33) {
                intent = new Intent(MediaStore.ACTION_PICK_IMAGES);
                intent.setType("image/*");
            } else {
                intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
                intent.addCategory(Intent.CATEGORY_OPENABLE);
                intent.setType("image/*");
            }
            try {
                startActivityForResult(intent, REQUEST);
            } catch (Exception e) {
                finish("");
            }
        }

        @Override public void onActivityResult(int requestCode, int resultCode, Intent data) {
            if (requestCode != REQUEST) return;
            Uri uri = (resultCode == Activity.RESULT_OK && data != null) ? data.getData() : null;
            if (uri == null) { finish(""); return; }
            final Activity activity = getActivity();
            final Uri picked = uri;
            new Thread(new Runnable() {
                @Override public void run() {
                    final String path = copyUpright(activity, picked);
                    activity.runOnUiThread(new Runnable() { @Override public void run() { finish(path); } });
                }
            }).start();
        }

        private void finish(String path) {
            Bundle args = getArguments();
            UnityPlayer.UnitySendMessage(args.getString("go"), args.getString("method"), path == null ? "" : path);
            try { getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss(); } catch (Exception ignored) { }
        }
    }

    static String copyUpright(Activity activity, Uri uri) {
        try {
            BitmapFactory.Options bounds = new BitmapFactory.Options();
            bounds.inJustDecodeBounds = true;
            InputStream in = activity.getContentResolver().openInputStream(uri);
            BitmapFactory.decodeStream(in, null, bounds);
            in.close();
            int sample = 1;
            while (Math.max(bounds.outWidth, bounds.outHeight) / (sample * 2) >= 1600) sample *= 2;

            BitmapFactory.Options opts = new BitmapFactory.Options();
            opts.inSampleSize = sample;
            in = activity.getContentResolver().openInputStream(uri);
            Bitmap bmp = BitmapFactory.decodeStream(in, null, opts);
            in.close();
            if (bmp == null) return "";

            int rotation = 0;
            boolean flip = false;
            in = activity.getContentResolver().openInputStream(uri);
            try {
                ExifInterface exif = new ExifInterface(in);
                switch (exif.getAttributeInt(ExifInterface.TAG_ORIENTATION, ExifInterface.ORIENTATION_NORMAL)) {
                    case ExifInterface.ORIENTATION_ROTATE_90: rotation = 90; break;
                    case ExifInterface.ORIENTATION_ROTATE_180: rotation = 180; break;
                    case ExifInterface.ORIENTATION_ROTATE_270: rotation = 270; break;
                    case ExifInterface.ORIENTATION_FLIP_HORIZONTAL: flip = true; break;
                }
            } catch (Exception ignored) {
            } finally {
                in.close();
            }

            float scale = Math.min(1f, 1600f / Math.max(bmp.getWidth(), bmp.getHeight()));
            if (rotation != 0 || flip || scale < 1f) {
                Matrix m = new Matrix();
                m.postScale(flip ? -scale : scale, scale);
                m.postRotate(rotation);
                Bitmap turned = Bitmap.createBitmap(bmp, 0, 0, bmp.getWidth(), bmp.getHeight(), m, true);
                if (turned != bmp) bmp.recycle();
                bmp = turned;
            }

            File out = new File(activity.getCacheDir(), "avatar_pick.jpg");
            FileOutputStream fos = new FileOutputStream(out);
            bmp.compress(Bitmap.CompressFormat.JPEG, 90, fos);
            fos.close();
            bmp.recycle();
            return out.getAbsolutePath();
        } catch (Exception e) {
            return "";
        }
    }
}
