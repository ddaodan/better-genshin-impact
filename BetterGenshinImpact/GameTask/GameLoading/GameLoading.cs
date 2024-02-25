﻿using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.GameTask.GameLoading.Assets;
using BetterGenshinImpact.Helpers;
using System;
using System.Diagnostics;

namespace BetterGenshinImpact.GameTask.GameLoading;

public class GameLoadingTrigger : ITaskTrigger
{
    public string Name => "自动开门";

    public bool IsEnabled { get; set; }

    public int Priority => 999;

    public bool IsExclusive => false;

    private readonly GameLoadingAssets _assets = new();

    private readonly GenshinStartConfig _config = TaskContext.Instance().Config.GenshinStartConfig;

    private int _enterGameClickCount = 0;
    private int _welkinMoonClickCount = 0;
    private int _noneClickCount = 0;

    private DateTime _prevExecuteTime = DateTime.MinValue;

    private ClickOffset? _clickOffset;

    public void Init()
    {
        IsEnabled = _config.AutoEnterGameEnabled;
        // 前面没有联动启动原神，这个任务也不用启动
        if ((DateTime.Now - TaskContext.Instance().LinkedStartGenshinTime).TotalMinutes >= 5)
        {
            IsEnabled = false;
        }

        _enterGameClickCount = 0;

        var captureArea = TaskContext.Instance().SystemInfo.CaptureAreaRect;
        var assetScale = TaskContext.Instance().SystemInfo.AssetScale;
         _clickOffset = new ClickOffset(captureArea.X, captureArea.Y, assetScale);
    }

    public void OnCapture(CaptureContent content)
    {
        // 5s 一次
        if ((DateTime.Now - _prevExecuteTime).TotalMilliseconds <= 5000)
        {
            return;
        }

        _prevExecuteTime = DateTime.Now;
        // 5min 后自动停止
        if ((DateTime.Now - TaskContext.Instance().LinkedStartGenshinTime).TotalMinutes >= 5)
        {
            IsEnabled = false;
            return;
        }

        using var ra = content.CaptureRectArea.Find(_assets.EnterGameRo);
        if (!ra.IsEmpty())
        {
            // 随便找个相对点击的位置
            _clickOffset?.Click(955, 666);
            _enterGameClickCount++;
        }
        else
        {
            if (_enterGameClickCount > 0 && !_config.AutoClickBlessingOfTheWelkinMoonEnabled)
            {
                _noneClickCount++;
                if (_noneClickCount > 5)
                {
                    IsEnabled = false;
                }
            }
        }


        if (_enterGameClickCount > 0 && _config.AutoClickBlessingOfTheWelkinMoonEnabled)
        {
            content.CaptureRectArea.Find(_assets.WelkinMoonRo, area =>
            {
                area.ClickCenter();
                _welkinMoonClickCount++;
                Debug.WriteLine("[GameLoading] Click blessing of the welkin moon");
                if (_welkinMoonClickCount > 2)
                {
                    IsEnabled = false;
                }
            });

        }
    }
}