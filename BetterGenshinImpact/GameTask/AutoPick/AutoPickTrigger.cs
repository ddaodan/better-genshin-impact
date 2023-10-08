﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.Core.Recognition.ONNX.SVTR;
using BetterGenshinImpact.Core.Recognition.OpenCv;
using BetterGenshinImpact.GameTask.AutoPick.Assets;
using BetterGenshinImpact.GameTask.Model;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using WindowsInput;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace BetterGenshinImpact.GameTask.AutoPick;

public class AutoPickTrigger : ITaskTrigger
{
    private readonly ILogger<AutoPickTrigger> _logger = App.GetLogger<AutoPickTrigger>();
    private readonly ITextInference _pickTextInference = TextInferenceFactory.Pick;

    public string Name => "自动拾取";
    public bool IsEnabled { get; set; }
    public int Priority => 30;
    public bool IsExclusive => false;

    private readonly AutoPickAssets _autoPickAssets;

    /// <summary>
    /// 拾取黑名单
    /// </summary>
    private List<string> _blackList = new();

    /// <summary>
    /// 拾取白名单
    /// </summary>
    private List<string> _whiteList = new();


    public AutoPickTrigger()
    {
        _autoPickAssets = new AutoPickAssets();
    }

    public void Init()
    {
        IsEnabled = TaskContext.Instance().Config.AutoPickConfig.Enabled;
        var blackListJson = Global.ReadAllTextIfExist("Config\\pick_black_lists.json");
        if (!string.IsNullOrEmpty(blackListJson))
        {
            _blackList = JsonSerializer.Deserialize<List<string>>(blackListJson) ?? new List<string>();
        }

        var whiteListJson = Global.ReadAllTextIfExist("Config\\pick_white_lists.json");
        if (!string.IsNullOrEmpty(whiteListJson))
        {
            _whiteList = JsonSerializer.Deserialize<List<string>>(whiteListJson) ?? new List<string>();
        }
    }

    public void OnCapture(CaptureContent content)
    {
        content.CaptureRectArea.Find(_autoPickAssets.FRo, foundRectArea =>
        {
            var scale = TaskContext.Instance().SystemInfo.AssetScale;
            var config = TaskContext.Instance().Config.AutoPickConfig;

            // 识别到F键，开始识别物品图标
            bool isChatIcon = false;
            _autoPickAssets.ChatIconRo.RegionOfInterest = new Rect(foundRectArea.X + (int)(config.ItemIconLeftOffset * scale), foundRectArea.Y, (int)((config.ItemTextLeftOffset - config.ItemIconLeftOffset) * scale), foundRectArea.Height);
            var iconRa = content.CaptureRectArea.Find(_autoPickAssets.ChatIconRo);
            if (!iconRa.IsEmpty())
            {
                // 物品图标是聊天气泡，一般是NPC对话，文字不在白名单不拾取
                isChatIcon = true;
            }

            // 这类文字识别比较特殊，都是针对某个场景的文字识别，所以暂时未抽象到识别对象中
            // 计算出文字区域
            var textRect = new Rect(foundRectArea.X + (int)(config.ItemTextLeftOffset * scale), foundRectArea.Y,
                (int)((config.ItemTextRightOffset - config.ItemTextLeftOffset) * scale), foundRectArea.Height);
            if (textRect.X + textRect.Width > content.CaptureRectArea.SrcGreyMat.Width
                || textRect.Y + textRect.Height > content.CaptureRectArea.SrcGreyMat.Height)
            {
                Debug.WriteLine("AutoPickTrigger: 文字区域 out of range");
                return;
            }

            var textMat = new Mat(content.CaptureRectArea.SrcGreyMat, textRect);

            var paddedMat = PreProcessForInference(textMat);
            var text = _pickTextInference.Inference(paddedMat);
            if (!string.IsNullOrEmpty(text))
            {
                if (_whiteList.Contains(text))
                {
                    _logger.LogInformation("交互或拾取：{Text}", text);
                    new InputSimulator().Keyboard.KeyPress(VirtualKeyCode.VK_F);
                    return;
                }

                if (isChatIcon)
                {
                    //Debug.WriteLine("AutoPickTrigger: 物品图标是聊天气泡，一般是NPC对话，不拾取");
                    return;
                }

                if (_blackList.Contains(text))
                {
                    return;
                }

                _logger.LogInformation("交互或拾取：{Text}", text);
                new InputSimulator().Keyboard.KeyPress(VirtualKeyCode.VK_F);
            }
        });
    }

    private Mat PreProcessForInference(Mat mat)
    {
        // 二值化
        Cv2.Threshold(mat, mat, 0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);
        //Cv2.AdaptiveThreshold(mat, mat, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 31, 3); // 效果不错 但是和模型不搭
        //mat = OpenCvCommonHelper.Threshold(mat, Scalar.FromRgb(235, 235, 235), Scalar.FromRgb(255, 255, 255)); // 识别物品不太行
        // 不知道为什么要强制拉伸到 221x32
        mat = ResizeHelper.ResizeTo(mat, 221, 32);
        // 填充到 384x32
        var padded = new Mat(new Size(384, 32), MatType.CV_8UC1, Scalar.Black);
        padded[new Rect(0, 0, mat.Width, mat.Height)] = mat;
        //Cv2.ImWrite(Global.Absolute("padded.png"), padded);
        return padded;
    }
}