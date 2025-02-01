﻿using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.Core.Script.WebView;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.Helpers;
using BetterGenshinImpact.Helpers.Http;
using BetterGenshinImpact.Model;
using BetterGenshinImpact.View.Controls.Webview;
using BetterGenshinImpact.ViewModel.Pages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Vanara.PInvoke;
using Wpf.Ui.Violeta.Controls;

namespace BetterGenshinImpact.Core.Script;

public class ScriptRepoUpdater : Singleton<ScriptRepoUpdater>
{
    private readonly ILogger<ScriptRepoUpdater> _logger = App.GetLogger<ScriptRepoUpdater>();

    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    // 仓储位置
    public static readonly string ReposPath = Global.Absolute("Repos");

    // 仓储临时目录 用于下载与解压
    public static readonly string ReposTempPath = Path.Combine(ReposPath, "Temp");

    // 中央仓库信息地址
    public static readonly string CenterRepoInfoUrl = "https://raw.githubusercontent.com/babalae/bettergi-scripts-list/refs/heads/main/repo.json";

    // 中央仓库解压后文件夹名
    public static readonly string CenterRepoUnzipName = "bettergi-scripts-list-main";

    public static readonly string CenterRepoPath = Path.Combine(ReposPath, CenterRepoUnzipName);

    public static readonly Dictionary<string, string> PathMapper = new Dictionary<string, string>
    {
        { "pathing", Global.Absolute("User\\AutoPathing") },
        { "js", Global.Absolute("User\\JsScript") },
        { "combat", Global.Absolute("User\\AutoFight") },
        { "tcg", Global.Absolute("User\\AutoGeniusInvokation") },
    };

    private WebpageWindow? _webWindow;

    public void AutoUpdate()
    {
        var scriptConfig = TaskContext.Instance().Config.ScriptConfig;

        if (!Directory.Exists(ReposPath))
        {
            Directory.CreateDirectory(ReposPath);
        }

        // 判断更新周期是否到达
        if (DateTime.Now - scriptConfig.LastUpdateScriptRepoTime >= TimeSpan.FromDays(scriptConfig.AutoUpdateScriptRepoPeriod))
        {
            // 更新仓库
            Task.Run(async () =>
            {
                try
                {
                    var (repoPath, updated) = await UpdateCenterRepo();
                    Debug.WriteLine($"脚本仓库更新完成，路径：{repoPath}");
                    scriptConfig.LastUpdateScriptRepoTime = DateTime.Now;
                    if (updated)
                    {
                        scriptConfig.ScriptRepoHintDotVisible = true;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogDebug(e, $"脚本仓库更新失败：{e.Message}");
                }
            });
        }
    }

    public async Task<(string, bool)> UpdateCenterRepo()
    {
        // 测速并获取信息
        var res = await ProxySpeedTester.GetFastestProxyAsync(CenterRepoInfoUrl);
        // 解析信息
        var fastProxyUrl = res.Item1;
        var jsonString = res.Item2;
        if (string.IsNullOrEmpty(jsonString))
        {
            throw new Exception("获取仓库信息失败");
        }
        
        var (time, url, file) = ParseJson(jsonString);

        var updated = false;

        // 检查仓库是否存在，不存在则下载
        var needDownload = false;
        if (Directory.Exists(CenterRepoPath))
        {
            var p = Directory.GetFiles(CenterRepoPath, "repo.json", SearchOption.AllDirectories).FirstOrDefault();
            if (p is null)
            {
                needDownload = true;
            }
        }
        else
        {
            needDownload = true;
        }
        if (needDownload)
        {
            await DownloadRepoAndUnzip(string.Format(fastProxyUrl, url));
            updated = true;
        }

        // 搜索本地的 repo.json
        var localRepoJsonPath = Directory.GetFiles(CenterRepoPath, file, SearchOption.AllDirectories).FirstOrDefault();
        if (localRepoJsonPath is null)
        {
            throw new Exception("本地仓库缺少 repo.json");
        }

        var (time2, url2, file2) = ParseJson(await File.ReadAllTextAsync(localRepoJsonPath));

        // 检查是否需要更新
        if (long.Parse(time) > long.Parse(time2))
        {
            await DownloadRepoAndUnzip(string.Format(fastProxyUrl, url2));
            updated = true;
        }

        // 获取与 localRepoJsonPath 同名（无扩展名）的文件夹路径
        var folderName = Path.GetFileNameWithoutExtension(localRepoJsonPath);
        var folderPath = Path.Combine(Path.GetDirectoryName(localRepoJsonPath)!, folderName);
        if (!Directory.Exists(folderPath))
        {
            throw new Exception("本地仓库文件夹不存在");
        }

        return (folderPath, updated);
    }

    public string FindCenterRepoPath()
    {
        var localRepoJsonPath = Directory.GetFiles(CenterRepoPath, "repo.json", SearchOption.AllDirectories).FirstOrDefault();
        if (localRepoJsonPath is null)
        {
            throw new Exception("本地仓库缺少 repo.json");
        }

        // 获取与 localRepoJsonPath 同名（无扩展名）的文件夹路径
        var folderName = Path.GetFileNameWithoutExtension(localRepoJsonPath);
        var folderPath = Path.Combine(Path.GetDirectoryName(localRepoJsonPath)!, folderName);
        if (!Directory.Exists(folderPath))
        {
            throw new Exception("本地仓库文件夹不存在");
        }

        return folderPath;
    }

    private (string time, string url, string file) ParseJson(string jsonString)
    {
        var json = JObject.Parse(jsonString);
        var time = json["time"]?.ToString();
        var url = json["url"]?.ToString();
        var file = json["file"]?.ToString();
        // 检查是否有空值
        if (time is null || url is null || file is null)
        {
            throw new Exception("repo.json 解析失败");
        }

        return (time, url, file);
    }

    public async Task DownloadRepoAndUnzip(string url)
    {
        // 下载
        var res = await _httpClient.GetAsync(url);
        if (!res.IsSuccessStatusCode)
        {
            throw new Exception("下载失败");
        }

        var bytes = await res.Content.ReadAsByteArrayAsync();

        // 获取文件名
        var contentDisposition = res.Content.Headers.ContentDisposition;
        var fileName = contentDisposition is { FileName: not null } ? contentDisposition.FileName.Trim('"') : "temp.zip";

        // 创建临时目录
        if (!Directory.Exists(ReposTempPath))
        {
            Directory.CreateDirectory(ReposTempPath);
        }

        // 保存下载的文件
        var zipPath = Path.Combine(ReposTempPath, fileName);
        await File.WriteAllBytesAsync(zipPath, bytes);

        // 删除旧文件夹
        if (Directory.Exists(CenterRepoPath))
        {
            DirectoryHelper.DeleteReadOnlyDirectory(CenterRepoPath);
        }

        // 使用 System.IO.Compression 解压
        ZipFile.ExtractToDirectory(zipPath, ReposPath, true);
    }

    public async Task ImportScriptFromClipboard()
    {
        // 获取剪切板内容
        try
        {
            if (Clipboard.ContainsText())
            {
                string clipboardText = Clipboard.GetText();
                await ImportScriptFromUri(clipboardText, true);
            }
        }
        catch (Exception e)
        {
            // 剪切板内容可能获取会失败
            Console.WriteLine(e);
        }
    }

    public async Task ImportScriptFromUri(string uri, bool formClipboard)
    {
        // 检查剪切板内容是否符合特定的URL格式
        if (!string.IsNullOrEmpty(uri) && uri.Trim().ToLower().StartsWith("bettergi://script?import="))
        {
            Debug.WriteLine($"脚本订购内容：{uri}");
            // 执行相关操作
            var pathJson = ParseUri(uri);
            if (!string.IsNullOrEmpty(pathJson))
            {
                var uiMessageBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "脚本订阅",
                    Content = $"检测到{(formClipboard ? "剪切板上存在" : "")}脚本订阅链接，解析后需要导入的脚本为：{pathJson}。\n是否导入并覆盖此文件或者文件夹下的脚本？",
                    CloseButtonText = "关闭",
                    PrimaryButtonText = "确认导入",
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                };

                var result = await uiMessageBox.ShowDialogAsync();
                if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
                {
                    await ImportScriptFromPathJson(pathJson);
                }

                // ContentDialog dialog = new()
                // {
                //     Title = "脚本订阅",
                //     Content = $"检测到{(formClipboard ? "剪切板上存在" : "")}脚本订阅链接，解析后需要导入的脚本为：{pathJson}。\n是否导入并覆盖此文件或者文件夹下的脚本？",
                //     CloseButtonText = "关闭",
                //     PrimaryButtonText = "确认导入",
                // };
                //
                // var result = await dialog.ShowAsync();
                // if (result == ContentDialogResult.Primary)
                // {
                //     await ImportScriptFromPathJson(pathJson);
                // }
            }

            if (formClipboard)
            {
                // 清空剪切板内容
                Clipboard.Clear();
            }
        }
    }

    private string? ParseUri(string uriString)
    {
        var uri = new Uri(uriString);

        // 获取 query 参数
        string query = uri.Query;
        Debug.WriteLine($"Query: {query}");

        // 解析 query 参数
        var queryParams = System.Web.HttpUtility.ParseQueryString(query);
        var import = queryParams["import"];
        if (string.IsNullOrEmpty(import))
        {
            Debug.WriteLine("未找到 import 参数");
            return null;
        }

        // Base64 解码后再使用url解码
        byte[] data = Convert.FromBase64String(import);
        return System.Web.HttpUtility.UrlDecode(System.Text.Encoding.UTF8.GetString(data));
    }

    public async Task ImportScriptFromPathJson(string pathJson)
    {
        var paths = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(pathJson);
        if (paths is null || paths.Count == 0)
        {
            Toast.Warning("订阅脚本路径为空");
            return;
        }

        // 保存订阅信息
        var scriptConfig = TaskContext.Instance().Config.ScriptConfig;
        scriptConfig.SubscribedScriptPaths.AddRange(paths);

        Toast.Information("获取最新仓库信息中...");

        // 更新仓库
        var (repoPath, _) = await Task.Run(UpdateCenterRepo);

        // // 收集将被覆盖的文件和文件夹
        // var filesToOverwrite = new List<string>();
        // foreach (var path in paths)
        // {
        //     var first = GetFirstFolder(path);
        //     if (PathMapper.TryGetValue(first, out var userPath))
        //     {
        //         var scriptPath = Path.Combine(repoPath, path);
        //         var destPath = Path.Combine(userPath, path.Replace(first, ""));
        //         if (Directory.Exists(scriptPath))
        //         {
        //             if (Directory.Exists(destPath))
        //             {
        //                 filesToOverwrite.Add(destPath);
        //             }
        //         }
        //         else if (File.Exists(scriptPath))
        //         {
        //             if (File.Exists(destPath))
        //             {
        //                 filesToOverwrite.Add(destPath);
        //             }
        //         }
        //     }
        //     else
        //     {
        //         Toast.Warning($"未知的脚本路径：{path}");
        //     }
        // }
        //
        // // 提示用户确认
        // if (filesToOverwrite.Count > 0)
        // {
        //     var message = "以下文件和文件夹将被覆盖:\n" + string.Join("\n", filesToOverwrite) + "\n是否覆盖所有文件和文件夹？";
        //     var uiMessageBox = new Wpf.Ui.Controls.MessageBox
        //     {
        //         Title = "确认覆盖",
        //         Content = message,
        //         CloseButtonText = "取消",
        //         PrimaryButtonText = "确认覆盖",
        //         WindowStartupLocation = WindowStartupLocation.CenterOwner,
        //         Owner = Application.Current.MainWindow,
        //     };
        //
        //     var result = await uiMessageBox.ShowDialogAsync();
        //     if (result != Wpf.Ui.Controls.MessageBoxResult.Primary)
        //     {
        //         return;
        //     }
        // }

        // 拷贝文件
        foreach (var path in paths)
        {
            var (first, remainingPath) = GetFirstFolderAndRemainingPath(path);
            if (PathMapper.TryGetValue(first, out var userPath))
            {
                var scriptPath = Path.Combine(repoPath, path);
                var destPath = Path.Combine(userPath, remainingPath);
                if (Directory.Exists(scriptPath))
                {
                    if (Directory.Exists(destPath))
                    {
                        DirectoryHelper.DeleteDirectoryWithReadOnlyCheck(destPath);
                    }

                    CopyDirectory(scriptPath, destPath);

                    // 图标处理
                    DealWithIconFolder(destPath);
                }
                else if (File.Exists(scriptPath))
                {
                    // 目标文件所在文件夹不存在时创建它
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    
                    if (File.Exists(destPath))
                    {
                        File.Delete(destPath);
                    }

                    File.Copy(scriptPath, destPath, true);
                }

                Toast.Success("脚本订阅链接导入完成");
            }
            else
            {
                Toast.Warning($"未知的脚本路径：{path}");
            }
        }
    }

    private void CopyDirectory(string sourceDir, string destDir)
    {
        // 创建目标目录
        Directory.CreateDirectory(destDir);

        // 拷贝文件
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        // 拷贝子目录
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
            // 图标处理
            DealWithIconFolder(destSubDir);
        }
    }

    private static (string firstFolder, string remainingPath) GetFirstFolderAndRemainingPath(string path)
    {
        // 检查路径是否为空或仅包含部分字符
        if (string.IsNullOrEmpty(path))
        {
            return (string.Empty, string.Empty);
        }

        // 使用路径分隔符分割路径
        string[] parts = path.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        // 返回第一个文件夹和剩余路径
        return parts.Length > 0 ? (parts[0], string.Join(Path.DirectorySeparatorChar, parts.Skip(1))) : (string.Empty, string.Empty);
    }

    public void OpenLocalRepoInWebView()
    {
        if (_webWindow is not { IsVisible: true })
        {
            _webWindow = new WebpageWindow
            {
                Title = "Genshin Copilot Scripts | BetterGI 脚本本地中央仓库",
                Width = 1366,
                Height = 768,
            };
            _webWindow.Closed += (s, e) => _webWindow = null;
            _webWindow.Panel!.DownloadFolderPath = MapPathingViewModel.PathJsonPath;
            _webWindow.NavigateToFile(Global.Absolute(@"Assets\Web\ScriptRepo\index.html"));
            _webWindow.Panel!.OnWebViewInitializedAction = () => _webWindow.Panel!.WebView.CoreWebView2.AddHostObjectToScript("repoWebBridge", new RepoWebBridge());
            _webWindow.Show();
        }
        else
        {
            _webWindow.Activate();
        }
    }

    /// <summary>
    /// 处理带有 icon.ico 和 desktop.ini 的文件夹
    /// </summary>
    /// <param name="folderPath"></param>
    private void DealWithIconFolder(string folderPath)
    {
        if (Directory.Exists(folderPath)
            && File.Exists(Path.Combine(folderPath, "desktop.ini")))
        {
            // 使用 Vanara 库中的 SetFileAttributes 函数设置文件夹属性
            if (Kernel32.SetFileAttributes(folderPath, FileFlagsAndAttributes.FILE_ATTRIBUTE_READONLY))
            {
                Debug.WriteLine($"成功将文件夹设置为只读: {folderPath}");
            }
            else
            {
                Debug.WriteLine($"无法设置文件夹为只读: {folderPath}");
            }
        }
    }
}
