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
        private readonly IBrowserLocalStorageBridge bridge;

        public BrowserLocalStorage(string key) : this(key, new UnityWebLocalStorageBridge()) { }

        public BrowserLocalStorage(string key, IBrowserLocalStorageBridge bridge)
        {
            this.key = key ?? throw new ArgumentNullException(nameof(key));
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public bool TryRead(out string value, out string reason)
        {
            return bridge.TryRead(key, out value, out reason);
        }

        public bool TryWrite(string value, out string reason)
        {
            return bridge.TryWrite(key, value, out reason);
        }
    }

    public interface IBrowserLocalStorageBridge
    {
        // A successful null value means the key is absent. Failures must return false with a reason.
        bool TryRead(string key, out string value, out string reason);
        bool TryWrite(string key, string value, out string reason);
    }

    public sealed class UnityWebLocalStorageBridge : IBrowserLocalStorageBridge
    {
        public bool TryRead(string key, out string value, out string reason)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                value = CloudWhaleLocalStorageGet(key);
                if (CloudWhaleLocalStorageDidLastReadFail() == 1)
                {
                    reason = "Browser local storage could not be read.";
                    return false;
                }

                reason = null;
                return true;
            }
            catch (Exception exception) { value = null; reason = exception.Message; return false; }
#else
            value = null;
            reason = "Browser local storage is only available in a Unity Web player.";
            return false;
#endif
        }

        public bool TryWrite(string key, string value, out string reason)
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
        [DllImport("__Internal")] private static extern int CloudWhaleLocalStorageDidLastReadFail();
        [DllImport("__Internal")] private static extern int CloudWhaleLocalStorageSet(string key, string value);
#endif
    }
}
