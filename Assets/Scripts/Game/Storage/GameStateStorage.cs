using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CloudWhale.Game
{
    public interface IStateStorage
    {
        bool TryRead(out string value, out string reason);
        bool TryWrite(string value, out string reason);
    }

    public static class GameStateSerializer
    {
        public static string Serialize(GameStateData data) => JsonUtility.ToJson(data);

        public static bool TryDeserialize(string value, out GameStateData data)
        {
            try
            {
                data = JsonUtility.FromJson<GameStateData>(value);
                if (data != null) data.NormalizeExtensions();
                return data != null;
            }
            catch (ArgumentException)
            {
                data = null;
                return false;
            }
        }
    }

    public static class GameStorageFactory
    {
        public static IStateStorage CreateDefault() => new BrowserLocalStorage("cloudwhale-island.state.v1");
    }

    public sealed class BrowserLocalStorage : IStateStorage
    {
        private readonly string key;
        public BrowserLocalStorage(string key) { this.key = key ?? throw new ArgumentNullException(nameof(key)); }

        public bool TryRead(out string value, out string reason)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { value = CloudWhaleLocalStorageGet(key); reason = null; return true; }
            catch (Exception exception) { value = null; reason = exception.Message; return false; }
#else
            value = null;
            reason = "Browser local storage is only available in a Unity Web player.";
            return false;
#endif
        }

        public bool TryWrite(string value, out string reason)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                if (CloudWhaleLocalStorageSet(key, value) == 1) { reason = null; return true; }
                reason = "Browser local storage rejected the write.";
                return false;
            }
            catch (Exception exception) { reason = exception.Message; return false; }
#else
            reason = "Browser local storage is only available in a Unity Web player.";
            return false;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern string CloudWhaleLocalStorageGet(string key);
        [DllImport("__Internal")] private static extern int CloudWhaleLocalStorageSet(string key, string value);
#endif
    }
}
