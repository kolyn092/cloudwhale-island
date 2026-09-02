mergeInto(LibraryManager.library, {
  CloudWhaleLocalStorageGet: function(key) {
    try {
      var value = window.localStorage.getItem(UTF8ToString(key));
      return value === null ? 0 : stringToNewUTF8(value);
    } catch (error) {
      return 0;
    }
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
