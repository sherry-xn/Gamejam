using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AudioConfigSetup
{
    private const string AudioFolder = "Assets/Audios/01_成品音效_待交付";
    private const string OutputPath = "Assets/Audios/AudioConfig.asset";

    [MenuItem("Tools/Audio/Setup AudioConfig")]
    public static void Setup()
    {
        var clips = LoadAllClips(AudioFolder);
        if (clips.Count == 0)
        {
            Debug.LogError("[AudioConfigSetup] 未找到任何 AudioClip，请检查路径: " + AudioFolder);
            return;
        }

        var config = ScriptableObject.CreateInstance<AudioConfig>();

        config.bgmEntries = BuildBGMEntries(clips);
        config.ambientEntries = BuildAmbientEntries(clips);
        config.sfxEntries = BuildSFXEntries(clips);

        string dir = Path.GetDirectoryName(OutputPath);
        if (!AssetDatabase.IsValidFolder(dir))
            Directory.CreateDirectory(dir);

        AssetDatabase.DeleteAsset(OutputPath);
        AssetDatabase.CreateAsset(config, OutputPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[AudioConfigSetup] 已创建 AudioConfig: {OutputPath}");
        Debug.Log($"[AudioConfigSetup] BGM: {config.bgmEntries.Length}, 环境音: {config.ambientEntries.Length}, 音效: {config.sfxEntries.Length}");
        Selection.activeObject = config;
    }

    private static Dictionary<string, AudioClip> LoadAllClips(string folder)
    {
        var result = new Dictionary<string, AudioClip>();
        string absFolder = Path.GetFullPath(folder);
        if (!Directory.Exists(absFolder)) return result;

        foreach (var path in Directory.GetFiles(absFolder, "*.wav"))
        {
            string assetPath = path.Replace("\\", "/");
            int idx = assetPath.IndexOf("Assets/");
            if (idx >= 0) assetPath = assetPath.Substring(idx);

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip != null)
                result[clip.name] = clip;
        }

        foreach (var path in Directory.GetFiles(absFolder, "*.ogg"))
        {
            string assetPath = path.Replace("\\", "/");
            int idx = assetPath.IndexOf("Assets/");
            if (idx >= 0) assetPath = assetPath.Substring(idx);

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip != null)
                result[clip.name] = clip;
        }

        return result;
    }

    private static AudioClip FindClip(Dictionary<string, AudioClip> clips, string keyword)
    {
        foreach (var kvp in clips)
        {
            if (kvp.Key.Contains(keyword))
                return kvp.Value;
        }
        return null;
    }

    private static SoundEntry MakeEntry(AudioClip clip, float volume = 1f, bool loop = false)
    {
        return new SoundEntry { clip = clip, volume = volume, pitchVariance = 0f, loop = loop };
    }

    // ===== BGM =====
    private static BGMEntry[] BuildBGMEntries(Dictionary<string, AudioClip> clips)
    {
        var list = new List<BGMEntry>();

        // BGM.MainMenu — 无对应文件，留空
        list.Add(new BGMEntry { key = BGM.MainMenu, sound = new SoundEntry() });

        list.Add(new BGMEntry { key = BGM.Exploration, sound = MakeEntry(FindClip(clips, "钟表嘀嗒"), 0.5f, true) });
        list.Add(new BGMEntry { key = BGM.MonsterNear, sound = MakeEntry(FindClip(clips, "心跳_紧张"), 0.7f, true) });
        list.Add(new BGMEntry { key = BGM.EscapeSuccess, sound = MakeEntry(FindClip(clips, "元素_心跳"), 0.6f, false) });

        return list.ToArray();
    }

    // ===== 环境音 =====
    private static AmbientEntry[] BuildAmbientEntries(Dictionary<string, AudioClip> clips)
    {
        var list = new List<AmbientEntry>();

        // None — 留空
        list.Add(new AmbientEntry { key = AmbientRoom.None, sound = new SoundEntry() });

        var ancientHouse = FindClip(clips, "古宅");
        var corridor = FindClip(clips, "走廊_底噪");
        var toilet = FindClip(clips, "厕所_底噪");
        var woodCreak = FindClip(clips, "木地板咯吱");

        // Hall / Classroom / Dorm 共用古宅底噪
        list.Add(new AmbientEntry { key = AmbientRoom.Hall, sound = MakeEntry(ancientHouse, 0.4f, true) });
        list.Add(new AmbientEntry { key = AmbientRoom.Corridor, sound = MakeEntry(corridor, 0.4f, true) });
        list.Add(new AmbientEntry { key = AmbientRoom.Classroom, sound = MakeEntry(ancientHouse, 0.4f, true) });
        list.Add(new AmbientEntry { key = AmbientRoom.Dorm, sound = MakeEntry(ancientHouse, 0.4f, true) });
        list.Add(new AmbientEntry { key = AmbientRoom.Toilet, sound = MakeEntry(toilet, 0.4f, true) });
        list.Add(new AmbientEntry { key = AmbientRoom.AncientHouse, sound = MakeEntry(woodCreak, 0.3f, true) });

        return list.ToArray();
    }

    // ===== SFX =====
    private static SFXEntry[] BuildSFXEntries(Dictionary<string, AudioClip> clips)
    {
        var list = new List<SFXEntry>();

        // 道具/机制
        list.Add(MakeSFX(SFX.DoorOpen, clips, "门_打开"));
        list.Add(MakeSFX(SFX.DoorClose, clips, "门_关闭"));
        list.Add(MakeSFX(SFX.KeyPickup, clips, "钥匙_拾取"));
        list.Add(MakeSFX(SFX.MainDoorUnlock, clips, "大门_开锁"));
        list.Add(MakeSFX(SFX.HideIn, clips, "角色_躲藏"));
        list.Add(MakeSFX(SFX.HideOut, clips, "角色_躲藏")); // 共用同一音效
        list.Add(MakeSFX(SFX.BagSearch, clips, "旅行袋_翻找"));
        list.Add(MakeSFX(SFX.GlassBreak, clips, "碎玻璃_踩踏"));
        list.Add(MakeSFX(SFX.TutorialHint, clips, "提示_教学"));

        // 玩家
        list.Add(MakeSFX(SFX.PlayerFootstep, clips, "玩家_行走"));
        list.Add(MakeSFX(SFX.PlayerHit, clips, "角色_受击"));

        // 怪物
        list.Add(MakeSFX(SFX.MonsterFootstep, clips, "追击_脚步"));
        list.Add(MakeSFX(SFX.MonsterGrowl, clips, "提示_远处"));
        list.Add(MakeSFX(SFX.MonsterWallHit, clips, "隔壁_撞墙"));
        list.Add(MakeSFX(SFX.MonsterChase, clips, "提示音_接近"));
        list.Add(MakeSFX(SFX.GameOver, clips, "系统_失败"));

        // UI
        list.Add(MakeSFX(SFX.UIHover, clips, "大厅_悬停"));
        list.Add(MakeSFX(SFX.UIClick, clips, "全局_点击"));
        list.Add(MakeSFX(SFX.UIPopupOpen, clips, "弹窗弹出"));
        list.Add(MakeSFX(SFX.UIPopupClose, clips, "弹窗关闭"));
        list.Add(MakeSFX(SFX.UIConfirmExit, clips, "退出提示"));

        // 监控
        list.Add(MakeSFX(SFX.MonitorOpen, clips, "监控_打开"));
        list.Add(MakeSFX(SFX.MonitorClose, clips, "监控_关闭"));
        list.Add(MakeSFX(SFX.MonitorCooldownDone, clips, "监控_冷却结束"));
        list.Add(MakeSFX(SFX.MonitorStatic, clips, "雪花_噪点"));
        list.Add(MakeSFX(SFX.MonitorSignalLost, clips, "信号断裂"));

        // HUD 提示
        list.Add(MakeSFX(SFX.KeyFound, clips, "提示_找到钥匙"));
        list.Add(MakeSFX(SFX.EmptyBag, clips, "提示_空旅行袋"));

        return list.ToArray();
    }

    private static SFXEntry MakeSFX(SFX key, Dictionary<string, AudioClip> clips, string keyword)
    {
        return new SFXEntry { key = key, sound = MakeEntry(FindClip(clips, keyword)) };
    }
}
