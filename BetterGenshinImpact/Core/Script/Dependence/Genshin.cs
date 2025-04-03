﻿using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.GameTask.AutoTrackPath;
using System.Threading.Tasks;
using BetterGenshinImpact.GameTask.Common.Job;
using Vanara.PInvoke;
using BetterGenshinImpact.GameTask.AutoFishing;
using BetterGenshinImpact.ViewModel.Pages;
using System;
using BetterGenshinImpact.GameTask.AutoPathing;
using BetterGenshinImpact.GameTask.Common.BgiVision;
using OpenCvSharp;
using static BetterGenshinImpact.GameTask.Common.TaskControl;
using BetterGenshinImpact.GameTask.Common.Map;
namespace BetterGenshinImpact.Core.Script.Dependence;

public class Genshin
{
    private RECT captureAreaRect = TaskContext.Instance().SystemInfo.CaptureAreaRect;

    /// <summary>
    /// 游戏宽度
    /// </summary>
    public int Width => captureAreaRect.Width;

    /// <summary>
    /// 游戏高度
    /// </summary>
    public int Height => captureAreaRect.Height;

    /// <summary>
    /// 游戏窗口大小相比1080P的缩放比例
    /// </summary>
    public double ScaleTo1080PRatio { get; } = TaskContext.Instance().SystemInfo.ScaleTo1080PRatio;

    /// <summary>
    /// 系统屏幕的DPI缩放比例
    /// </summary>
    public double ScreenDpiScale => TaskContext.Instance().DpiScale;

    /// <summary>
    /// 传送到指定位置
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public async Task Tp(double x, double y)
    {
        await new TpTask(CancellationContext.Instance.Cts.Token).Tp(x, y);
    }

    public async Task Tp(double x, double y, bool force)
    {
        await new TpTask(CancellationContext.Instance.Cts.Token).Tp(x, y, force);
    }

    /// <summary>
    /// 传送到指定位置
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public async Task Tp(string x, string y)
    {
        double.TryParse(x, out var dx);
        double.TryParse(y, out var dy);
        await Tp(dx, dy);
    }

    public async Task Tp(string x, string y, bool force)
    {
        double.TryParse(x, out var dx);
        double.TryParse(y, out var dy);
        await Tp(dx, dy, force);
    }

        
    #region 大地图操作
    /// <summary>
    /// 移动大地图到指定坐标
    /// </summary>
    /// <remarks>
    /// 与内置传送功能不同，此方法不会多次重试。
    /// 为避免初次中心点识别失败，建议先使用 SetBigMapZoomLevel 设置合适的大地图缩放等级。
    /// </remarks>
    /// <param name="x">目标X坐标</param>
    /// <param name="y">目标Y坐标</param>
    /// <param name="forceCountry">强制指定移动大地图时先切换的国家，默认为null</param>
    public async Task MoveMapTo(int x, int y, string? forceCountry = null)
    {
        TpTask tpTask = new TpTask(CancellationContext.Instance.Cts.Token);
        await tpTask.CheckInBigMapUi();
        await tpTask.SwitchRecentlyCountryMap(x, y, forceCountry);
        await tpTask.MoveMapTo(x, y);
    }

    /// <summary>
    /// 获取当前大地图缩放等级
    /// </summary>
    /// <returns>当前大地图缩放等级，范围1.0-6.0</returns>
    public double GetBigMapZoomLevel()
    {
        TpTask tpTask = new(CancellationContext.Instance.Cts.Token);        
        return tpTask.GetBigMapZoomLevel(CaptureToRectArea());
    }

    /// <summary>
    /// 将大地图缩放等级设置为指定值
    /// </summary>
    /// <remarks>
    /// 缩放等级说明：
    /// - 数值范围：1.0(最大地图) 到 6.0(最小地图)
    /// - 缩放效果：数值越大，地图显示范围越广，细节越少
    /// - 缩放位置：1.0 对应缩放条最上方，6.0 对应缩放条最下方
    /// - 推荐范围：建议在 2.0 到 5.0 之间调整，过大或过小可能影响操作
    /// </remarks>
    /// <param name="zoomLevel">目标缩放等级，范围 1.0-6.0</param>
    public async Task SetBigMapZoomLevel(double zoomLevel)
    {
        TpTask tpTask = new(CancellationContext.Instance.Cts.Token);
        double currentZoomLevel = GetBigMapZoomLevel();
        await tpTask.AdjustMapZoomLevel(currentZoomLevel, zoomLevel);
    }
    
    /// <summary>
    /// 传送到用户指定的七天神像
    /// </summary>
    public async Task TpToStatueOfTheSeven()
    {
        TpTask tpTask = new TpTask(CancellationContext.Instance.Cts.Token);
        await tpTask.TpToStatueOfTheSeven();
    }

    /// <summary>
    /// 获取当前在大地图上的位置坐标
    /// </summary>
    /// <returns>包含X和Y坐标的Point2f结构体</returns>
    public Point2f GetPositionFromBigMap()
    {
        TpTask tpTask = new TpTask(CancellationContext.Instance.Cts.Token);
        return tpTask.GetPositionFromBigMap();
    }

    /// <summary>
    /// 获取当前在小地图上的位置坐标
    /// </summary>
    /// <returns>包含X和Y坐标的Point2f结构体</returns>
    public Point2f GetPositionFromMap(){
        var imageRegion = CaptureToRectArea();
        if (!Bv.IsInMainUi(imageRegion))
        {
            throw new InvalidOperationException("不在主界面，无法识别小地图坐标");
        }
        return MapCoordinate.Main2048ToGame(Navigation.GetPositionStable(imageRegion));
    }

    #endregion 大地图操作
    /// <summary>
    /// 切换队伍
    /// </summary>
    /// <param name="partyName">队伍界面自定义的队伍名称</param>
    /// <returns></returns>
    public async Task SwitchParty(string partyName)
    {
        await new SwitchPartyTask().Start(partyName, CancellationContext.Instance.Cts.Token);
    }


    /// <summary>
    /// 自动点击空月祝福
    /// </summary>
    /// <returns></returns>
    public async Task BlessingOfTheWelkinMoon()
    {
        await new BlessingOfTheWelkinMoonTask().Start(CancellationContext.Instance.Cts.Token);
    }

    /// <summary>
    /// 持续对话并选择目标选项
    /// </summary>
    /// <param name="option">选项文本</param>
    /// <param name="skipTimes">跳过次数</param>
    /// <param name="isOrange">是否为橙色选项</param>
    /// <returns></returns>
    public async Task ChooseTalkOption(string option, int skipTimes = 10, bool isOrange = false)
    {
        await new ChooseTalkOptionTask().SingleSelectText(option, CancellationContext.Instance.Cts.Token, skipTimes, isOrange);
    }

    /// <summary>
    /// 一键领取纪行奖励
    /// </summary>
    /// <returns></returns>
    public async Task ClaimBattlePassRewards()
    {
        await new ClaimBattlePassRewardsTask().Start(CancellationContext.Instance.Cts.Token);
    }

    /// <summary>
    /// 领取长效历练点奖励
    /// </summary>
    /// <returns></returns>
    public async Task ClaimEncounterPointsRewards()
    {
        await new ClaimEncounterPointsRewardsTask().Start(CancellationContext.Instance.Cts.Token);
    }

    /// <summary>
    /// 前往冒险家协会领取奖励
    /// </summary>
    /// <param name="country">国家名称</param>
    /// <returns></returns>
    public async Task GoToAdventurersGuild(string country)
    {
        await new GoToAdventurersGuildTask().Start(country, CancellationContext.Instance.Cts.Token);
    }

    /// <summary>
    /// 前往合成台
    /// </summary>
    /// <param name="country">国家名称</param>
    /// <returns></returns>
    public async Task GoToCraftingBench(string country)
    {
        await new GoToCraftingBenchTask().Start(country, CancellationContext.Instance.Cts.Token);
    }

    /// <summary>
    /// 返回主界面
    /// </summary>
    /// <returns></returns>
    public async Task ReturnMainUi()
    {
        await new ReturnMainUiTask().Start(CancellationContext.Instance.Cts.Token);
    }

    /// <summary>
    /// 钓鱼
    /// </summary>
    /// <returns></returns>
    public async Task AutoFishing(int fishingTimePolicy = 0)
    {
        var taskSettingsPageViewModel = App.GetService<TaskSettingsPageViewModel>();
        if (taskSettingsPageViewModel == null)
        {
            throw new ArgumentNullException(nameof(taskSettingsPageViewModel), "内部视图模型对象为空");
        }

        var param = AutoFishingTaskParam.BuildFromConfig(TaskContext.Instance().Config.AutoFishingConfig, taskSettingsPageViewModel.SaveScreenshotOnKeyTick);
        param.FishingTimePolicy = (FishingTimePolicy)fishingTimePolicy;
        await new AutoFishingTask(param).Start(CancellationContext.Instance.Cts.Token);
    }
    
    /// <summary>
    /// 重新登录原神
    /// </summary>
    /// <returns></returns>
    public async Task Relogin()
    {
        await new ExitAndReloginJob().Start(CancellationContext.Instance.Cts.Token);
    }
}