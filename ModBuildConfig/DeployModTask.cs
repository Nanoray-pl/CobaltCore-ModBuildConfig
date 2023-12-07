using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Shockah.CobaltCoreModBuildConfig;

public class DeployModTask : Task
{
    [Required]
    public string ModName { get; set; } = null!;

    [Required]
    public string ModVersion { get; set; } = null!;

    [Required]
    public string ProjectDir { get; set; } = null!;

    [Required]
    public string TargetDir { get; set; } = null!;

    [Required]
    public bool EnableModDeploy { get; set; }

    [Required]
    public string ModDeployModsPath { get; set; } = null!;

    [Required]
    public bool EnableModZip { get; set; }

    [Required]
    public string ModZipPath { get; set; } = null!;

    public string IncludedModProjectPaths { get; set; } = "";

    public override bool Execute()
    {
        if (!EnableModDeploy && !EnableModZip)
            return true;

        var modFiles = GetModFiles(TargetDir, ProjectDir).ToList();

        if (EnableModDeploy)
            DeployMod(modFiles, Path.Combine(ModDeployModsPath, ModName));
        if (EnableModZip)
            ZipMod(modFiles, ModZipPath, ModName);

        return true;
    }

    private IEnumerable<(FileInfo Info, string RelativeName)> GetModFiles(string targetDir, string projectDir)
    {
        Uri projectDirUri = new(projectDir);

        static IEnumerable<(FileInfo Info, string RelativeName)> GetAllFilesFromDirectory(DirectoryInfo dirInfo)
        {
            Uri dirUri = new(dirInfo.FullName);
            foreach (FileInfo file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                Uri fileUri = new(file.FullName);
                string relativeName = dirUri.MakeRelativeUri(fileUri).OriginalString;
                yield return (Info: file, RelativeName: relativeName);
            }
        }

        foreach (var file in GetAllFilesFromDirectory(new(targetDir)))
            yield return file;

        IncludedModProjectPaths = IncludedModProjectPaths.Trim();
        if (IncludedModProjectPaths != "")
        {
            foreach (string includedProjectPath in IncludedModProjectPaths.Split(';'))
            {
                string path = Path.Combine(projectDir, includedProjectPath);
                if (Directory.Exists(path))
                {
                    DirectoryInfo dirInfo = new(path);
                    foreach (var file in GetAllFilesFromDirectory(dirInfo))
                        yield return file;
                }
                else if (File.Exists(path))
                {
                    Uri fileUri = new(path);
                    string relativeName = projectDirUri.MakeRelativeUri(fileUri).OriginalString;
                    yield return (new FileInfo(path), relativeName);
                }
            }
        }
    }

    private void DeployMod(IEnumerable<(FileInfo Info, string RelativeName)> modFiles, string destinationDir)
    {
        foreach (var (fileInfo, fileRelativeName) in modFiles)
        {
            string fromPath = fileInfo.FullName;
            string toPath = Path.Combine(destinationDir, fileRelativeName);

            Directory.CreateDirectory(Path.GetDirectoryName(toPath));
            File.Copy(fromPath, toPath, overwrite: true);
        }
    }

    private void ZipMod(IEnumerable<(FileInfo Info, string RelativeName)> modFiles, string destinationFile, string? innerDirName)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
        using Stream zipStream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write);
        using ZipArchive archive = new(zipStream, ZipArchiveMode.Create);

        foreach (var (fileInfo, fileRelativeName) in modFiles)
        {
            string fromPath = fileInfo.FullName;
            string zipEntryName = fileRelativeName.Replace(Path.DirectorySeparatorChar, '/');
            if (innerDirName is not null)
                zipEntryName = $"{innerDirName}/{zipEntryName}";

            using Stream fileStream = new FileStream(fromPath, FileMode.Open, FileAccess.Read);
            using Stream fileStreamInZip = archive.CreateEntry(zipEntryName).Open();
            fileStream.CopyTo(fileStreamInZip);
        }
    }
}
