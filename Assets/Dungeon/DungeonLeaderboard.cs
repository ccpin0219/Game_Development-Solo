using System;
using System.Collections.Generic;
using UnityEngine;

// Local records survive scene reloads and application restarts.
public class DungeonLeaderboard
{
    [Serializable]
    public class Entry
    {
        public string playerName;
        public float seconds;
        public int deaths;
    }
    [Serializable]
    class SaveData { public List<Entry> entries = new List<Entry>(); }
    readonly string storageKey;
    SaveData data;
    public IReadOnlyList<Entry> Entries => data.entries;

    public DungeonLeaderboard(string key = "AshenSanctum.Leaderboard.v1")
    {
        storageKey=key;
        try { data=JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(key,"")); }
        catch(ArgumentException) { data=null; }
        if(data==null)data=new SaveData();
        if(data.entries==null)data.entries=new List<Entry>();
        data.entries.RemoveAll(e=>e==null || string.IsNullOrWhiteSpace(e.playerName) ||
            float.IsNaN(e.seconds) || float.IsInfinity(e.seconds) || e.seconds<0 || e.deaths<0);
        data.entries.Sort((a,b)=>a.seconds.CompareTo(b.seconds));
    }
    public bool Add(string name,float seconds,int deaths)
    {
        if(string.IsNullOrWhiteSpace(name) || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds<0 || deaths<0)return false;
        name=name.Trim();
        if(name.Length>16)name=name.Substring(0,16);
        var entry=new Entry{playerName=name,seconds=seconds,deaths=deaths};
        // Insert after equal times; death count never affects sorting or ties.
        int index=data.entries.FindIndex(e=>e.seconds>seconds);
        if(index<0)data.entries.Add(entry);else data.entries.Insert(index,entry);
        PlayerPrefs.SetString(storageKey,JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        return true;
    }
    public int RankAt(int index)
    {
        int first=index;
        while(first>0 && data.entries[first-1].seconds==data.entries[index].seconds)first--;
        return first+1;
    }
}
