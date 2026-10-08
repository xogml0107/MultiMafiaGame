using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ZZabmongus.Editor
{
    /// <summary>Packages require fresh user approval. Never runs on import or domain reload.</summary>
    public static class NetworkPackageInstaller
    {
        public static readonly string[] Packages =
        {
            "https://github.com/PurrNet/PurrNet.git?path=/Assets/PurrNet#v1.24.1",
            "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1"
        };
        private static AddAndRemoveRequest request;
        private static double deadline;

        [MenuItem("ZZabmongus/Install Pinned Network Packages")]
        public static void Install()
        {
            if (Application.isBatchMode)
                throw new InvalidOperationException("Package installation is disabled in batch mode. Ask the user before changing packages.");
            if (request != null) throw new InvalidOperationException("A package request is already active.");
            if (!EditorUtility.DisplayDialog("패키지 설치 확인", "PurrNet 1.24.1과 Steamworks.NET 2025.164.1을 설치/재설치할까요?\n패키지 변경에는 사용자 승인이 필요합니다.", "설치", "취소")) return;
            request = Client.AddAndRemove(Packages);
            deadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update += Poll;
            Debug.Log("[ZZabmongus Packages] Installing approved packages.");
        }

        private static void Poll()
        {
            if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Poll;
            var success = request.IsCompleted && request.Status == StatusCode.Success;
            var message = success ? string.Join("\n", request.Result.Select(p => p.name + "@" + p.version)) :
                request.Error?.message ?? "Package installation timed out.";
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/zzabmongus-package-install.txt", (success ? "SUCCESS\n" : "FAILED\n") + message);
            request = null;
            if (success) Debug.Log("[ZZabmongus Packages] " + message);
            else Debug.LogError("[ZZabmongus Packages] " + message);
        }
    }
}
