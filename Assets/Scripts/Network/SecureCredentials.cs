using System;
using UnityEngine;
#if (UNITY_IOS && !UNITY_EDITOR) || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
using System.Runtime.InteropServices;
#endif

public static class SecureCredentials
{
    [Serializable] class LoginData { public string email; public string password; }
    public enum LoadResult { Missing, Available, Unavailable }
#if UNITY_EDITOR && !UNITY_EDITOR_OSX
    static string editorValue;
#endif
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] static extern int OfficeCredentialsSave(string value);
    [DllImport("__Internal")] static extern IntPtr OfficeCredentialsLoad();
    [DllImport("__Internal")] static extern int OfficeCredentialsClear();
    [DllImport("__Internal")] static extern void OfficeCredentialsFree(IntPtr value);
#endif
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
    [DllImport("OfficeCredentials")] static extern int OfficeCredentialsSave(string value);
    [DllImport("OfficeCredentials")] static extern IntPtr OfficeCredentialsLoad();
    [DllImport("OfficeCredentials")] static extern int OfficeCredentialsClear();
    [DllImport("OfficeCredentials")] static extern void OfficeCredentialsFree(IntPtr value);
#endif
    public static bool Save(string email, string password)
    {
        string value = JsonUtility.ToJson(new LoginData { email = email, password = password });
        try
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            return OfficeCredentialsSave(value) == 1;
#elif UNITY_EDITOR
            editorValue = value; return true; // In-memory only; never persist editor credentials.
#elif UNITY_ANDROID
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var store = new AndroidJavaClass("com.officeclash.security.OfficeCredentials"))
                return store.CallStatic<bool>("save", activity, value);
#elif UNITY_IOS
            return OfficeCredentialsSave(value) == 1;
#else
            return false;
#endif
        }
        catch (Exception) { return false; }
    }

    public static LoadResult Load(out string email, out string password)
    {
        email = null; password = null;
        try
        {
            string value;
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            IntPtr pointer = OfficeCredentialsLoad();
            if (pointer == IntPtr.Zero) return LoadResult.Missing;
            try { value = Marshal.PtrToStringUTF8(pointer); }
            finally { OfficeCredentialsFree(pointer); }
#elif UNITY_EDITOR
            value = editorValue;
#elif UNITY_ANDROID
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var store = new AndroidJavaClass("com.officeclash.security.OfficeCredentials"))
                value = store.CallStatic<string>("load", activity);
#elif UNITY_IOS
            IntPtr pointer = OfficeCredentialsLoad();
            if (pointer == IntPtr.Zero) return LoadResult.Missing;
            try { value = System.Runtime.InteropServices.Marshal.PtrToStringUTF8(pointer); }
            finally { OfficeCredentialsFree(pointer); }
#else
            return LoadResult.Missing;
#endif
            if (value == null) return LoadResult.Missing;
            if (string.IsNullOrEmpty(value)) return LoadResult.Unavailable;
            var login = JsonUtility.FromJson<LoginData>(value);
            if (login == null || string.IsNullOrWhiteSpace(login.email) || string.IsNullOrEmpty(login.password)) return LoadResult.Unavailable;
            email = login.email; password = login.password;
            return LoadResult.Available;
        }
        catch (Exception) { return LoadResult.Unavailable; }
    }

    public static bool Clear()
    {
        try
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            return OfficeCredentialsClear() == 1;
#elif UNITY_EDITOR
            editorValue = null; return true;
#elif UNITY_ANDROID
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var store = new AndroidJavaClass("com.officeclash.security.OfficeCredentials"))
                return store.CallStatic<bool>("clear", activity);
#elif UNITY_IOS
            return OfficeCredentialsClear() == 1;
#else
            return true;
#endif
        }
        catch (Exception) { return false; }
    }
}
