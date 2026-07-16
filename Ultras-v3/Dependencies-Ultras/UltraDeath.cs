/*
name: UltraDeath
description: Army death sync — detects and propagates death events across army members.
tags: ultra, death, sync, army
*/

//cs_include Scripts/Ultras-v3/Dependencies-Core/CoreBots2.cs
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/CoreUltra2.cs

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Skua.Core.Interfaces;

public class UltraDeath
{
    private IScriptInterface Bot => IScriptInterface.Instance;
    private CoreBots2 C => CoreBots2.Instance;
    private CoreUltra2 Ultra => _Ultra ??= new CoreUltra2();
    private CoreUltra2 _Ultra;

    private const string DeathSyncFile = "ultra_death.sync";

    /// <summary>
    /// Signals that this player has died. Writes the death timestamp to the sync file.
    /// </summary>
    public void SignalDeath()
    {
        string path = Ultra.ResolveSyncPath(DeathSyncFile);
        string key = $"{Bot.Player.Username}|death";
        Ultra.UpdateEntry(path, key, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
    }

    /// <summary>
    /// Returns true if any army member has died recently (within the stale threshold).
    /// </summary>
    public bool HasDeathOccurred()
    {
        string path = Ultra.ResolveSyncPath(DeathSyncFile);
        string[] lines = Ultra.ReadLines(path);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        const int staleThreshold = 30;

        foreach (string line in lines)
        {
            string[] parts = line.Split(':');
            if (parts.Length < 2)
                continue;

            if (!long.TryParse(parts[1], out long ts))
                continue;

            if (now - ts <= staleThreshold)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Clears all death entries from the sync file.
    /// </summary>
    public void ClearDeaths()
    {
        Ultra.ClearSyncFile(Ultra.ResolveSyncPath(DeathSyncFile));
    }

    /// <summary>
    /// Async loop that monitors this player's death and signals the army.
    /// Call with a CancellationToken to stop it.
    /// </summary>
    public async Task MonitorDeathAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!Bot.Player.Alive)
                {
                    SignalDeath();
                    C.Logger("[UltraDeath] Death detected, signaling army.");
                }

                await Task.Delay(500, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Waits (async, polling) until any player in the army has died, up to <paramref name="timeoutSecs"/>.
    /// Returns true if a death was detected, false on timeout.
    /// </summary>
    public async Task<bool> WaitForDeathAsync(int timeoutSecs = 60, CancellationToken token = default)
    {
        var start = DateTime.UtcNow;

        while (!token.IsCancellationRequested)
        {
            if (HasDeathOccurred())
                return true;

            if ((DateTime.UtcNow - start).TotalSeconds >= timeoutSecs)
                return false;

            try
            {
                await Task.Delay(500, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        return false;
    }
}
