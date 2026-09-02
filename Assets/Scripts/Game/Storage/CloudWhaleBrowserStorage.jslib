mergeInto(LibraryManager.library, {
  CloudWhaleLocalStorageGet: function(key) {
    try {
      var value = window.localStorage.getItem(UTF8ToString(key));
      globalThis.cloudWhaleLocalStorageReadFailed = 0;
      return value === null ? 0 : stringToNewUTF8(value);
    } catch (error) {
      globalThis.cloudWhaleLocalStorageReadFailed = 1;
      return 0;
    }
  },
  CloudWhaleLocalStorageDidLastReadFail: function() {
    return globalThis.cloudWhaleLocalStorageReadFailed === 1 ? 1 : 0;
  },
  CloudWhaleLocalStorageSet: function(key, value) {
    try {
      window.localStorage.setItem(UTF8ToString(key), UTF8ToString(value));
      return 1;
    } catch (error) {
      return 0;
    }
  }
});
