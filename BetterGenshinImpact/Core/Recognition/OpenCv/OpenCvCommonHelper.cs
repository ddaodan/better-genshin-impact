﻿using System;
using System.Collections.Generic;
using OpenCvSharp;
using System.Diagnostics;
using System.Linq;

namespace BetterGenshinImpact.Core.Recognition.OpenCv;

public class OpenCvCommonHelper
{
    /// <summary>
    ///     计算灰度图中某个颜色的像素个数
    /// </summary>
    /// <param name="mat"></param>
    /// <param name="color"></param>
    /// <returns></returns>
    public static int CountGrayMatColor(Mat mat, byte color)
    {
        Debug.Assert(mat.Depth() == MatType.CV_8U);
        return SplitChannal(mat, m => CountGrayMatColorC1(m, color)).Sum();
    }

    private static IEnumerable<T> SplitChannal<T>(Mat mat, Func<Mat, T> func)
    {
        if (mat.Empty()) return [];
        if (mat.Channels() == 1)
        {
            return [func.Invoke(mat)];
        }

        Mat[]? channels = null;
        try
        {
            channels = mat.Split();
            return channels.AsParallel().Select(func);
        }
        finally
        {
            // 释放所有分离出的通道内存
            if (channels is not null)
                foreach (var ch in channels)
                    ch.Dispose();
        }
    }

    /// <summary>
    /// 仅限单通道,统计等于color的颜色
    /// </summary>
    /// <param name="mat">矩阵</param>
    /// <param name="color">值</param>
    /// <returns></returns>
    public static int CountGrayMatColorC1(Mat mat, byte color)
    {
        Debug.Assert(mat.Depth() == MatType.CV_8U);
        Debug.Assert(mat.Channels() == 1);
        using var dst = new Mat();
        Cv2.Compare(mat, color, dst, CmpType.EQ);
        return Cv2.CountNonZero(dst);
    }

    /// <summary>
    /// 仅限单通道,统计颜色在lowColor和highColor中范围
    /// </summary>
    /// <param name="mat">矩阵</param>
    /// <param name="lowColor">低</param>
    /// <param name="highColor">高</param>
    /// <returns></returns>
    public static int CountGrayMatColorC1(Mat mat, byte lowColor, byte highColor)
    {
        using var mask = new Mat();
        // 使用InRange直接生成二值掩膜
        Cv2.InRange(mat, new Scalar(lowColor), new Scalar(highColor), mask);
        return Cv2.CountNonZero(mask);
    }

    public static int CountGrayMatColor(Mat mat, byte lowColor, byte highColor)
    {
        return SplitChannal(mat, m => CountGrayMatColorC1(m, lowColor, highColor)).Sum();
    }

    public static Mat Threshold(Mat src, Scalar low, Scalar high)
    {
        using var mask = new Mat();
        using var rgbMat = new Mat();

        Cv2.CvtColor(src, rgbMat, ColorConversionCodes.BGR2RGB);
        Cv2.InRange(rgbMat, low, high, mask);
        // Cv2.Threshold(mask, mask, 0, 255, ThresholdTypes.Binary); //二值化 //不需要
        return mask.Clone();
    }

    public static Mat InRangeHsv(Mat src, Scalar low, Scalar high)
    {
        using var mask = new Mat();
        using var rgbMat = new Mat();

        Cv2.CvtColor(src, rgbMat, ColorConversionCodes.BGR2HSV);
        Cv2.InRange(rgbMat, low, high, mask);
        return mask.Clone();
    }

    public static Mat Threshold(Mat src, Scalar s)
    {
        return Threshold(src, s, s);
    }

    /// <summary>
    ///     和二值化的颜色刚好相反
    /// </summary>
    /// <param name="src"></param>
    /// <param name="s"></param>
    /// <returns></returns>
    public static Mat CreateMask(Mat src, Scalar s)
    {
        var mask = new Mat();
        Cv2.InRange(src, s, s, mask);
        return ~ mask;
    }
}
