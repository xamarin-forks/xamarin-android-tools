﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Tools
{
	class AndroidSdkWindows : AndroidSdkBase
	{
		const string MDREG_KEY = @"SOFTWARE\Novell\Mono for Android";
		const string MDREG_ANDROID_SDK = "AndroidSdkDirectory";
		const string MDREG_ANDROID_NDK = "AndroidNdkDirectory";
		const string MDREG_JAVA_SDK = "JavaSdkDirectory";
		const string ANDROID_INSTALLER_PATH = @"SOFTWARE\Android SDK Tools";
		const string ANDROID_INSTALLER_KEY = "Path";
		const string XAMARIN_ANDROID_INSTALLER_PATH = @"SOFTWARE\Xamarin\MonoAndroid";
		const string XAMARIN_ANDROID_INSTALLER_KEY = "PrivateAndroidSdkPath";

		public AndroidSdkWindows (Action<TraceLevel, string> logger)
			: base (logger)
		{
		}

		public override string ZipAlign { get; protected set; } = "zipalign.exe";
		public override string JarSigner { get; protected set; } = "jarsigner.exe";
		public override string KeyTool { get; protected set; } = "keytool.exe";

		public override string NdkHostPlatform32Bit { get { return "windows"; } }
		public override string NdkHostPlatform64Bit { get { return "windows-x86_64"; } }
		public override string Javac { get; protected set; } = "javac.exe";

		public override string PreferedAndroidSdkPath {
			get {
				var wow = RegistryEx.Wow64.Key32;
				var regKey = GetMDRegistryKey ();
				if (CheckRegistryKeyForExecutable (RegistryEx.CurrentUser, regKey, MDREG_ANDROID_SDK, wow, "platform-tools", Adb))
					return RegistryEx.GetValueString (RegistryEx.CurrentUser, regKey, MDREG_ANDROID_SDK, wow);
				return null;
			}
		}
		public override string PreferedAndroidNdkPath {
			get {
				var wow = RegistryEx.Wow64.Key32;
				var regKey = GetMDRegistryKey ();
				if (CheckRegistryKeyForExecutable (RegistryEx.CurrentUser, regKey, MDREG_ANDROID_NDK, wow, ".", NdkStack))
					return RegistryEx.GetValueString (RegistryEx.CurrentUser, regKey, MDREG_ANDROID_NDK, wow);
				return null;
			}
		}
		public override string PreferedJavaSdkPath {
			get {
				var wow = RegistryEx.Wow64.Key32;
				var regKey = GetMDRegistryKey ();
				if (CheckRegistryKeyForExecutable (RegistryEx.CurrentUser, regKey, MDREG_JAVA_SDK, wow, "bin", JarSigner))
					return RegistryEx.GetValueString (RegistryEx.CurrentUser, regKey, MDREG_JAVA_SDK, wow);
				return null;
			}
		}

		string GetMDRegistryKey ()
		{
			var regKey = Environment.GetEnvironmentVariable ("XAMARIN_ANDROID_REGKEY");
			return string.IsNullOrWhiteSpace (regKey) ? MDREG_KEY : regKey;
		}

		protected override IEnumerable<string> GetAllAvailableAndroidSdks ()
		{
			var roots = new[] { RegistryEx.CurrentUser, RegistryEx.LocalMachine };
			var wow = RegistryEx.Wow64.Key32;
			var regKey = GetMDRegistryKey ();

			Logger (TraceLevel.Info, "Looking for Android SDK...");

			// Check for the key the user gave us in the VS/addin options
			foreach (var root in roots)
				if (CheckRegistryKeyForExecutable (root, regKey, MDREG_ANDROID_SDK, wow, "platform-tools", Adb))
					yield return RegistryEx.GetValueString (root, regKey, MDREG_ANDROID_SDK, wow);

			// Check for the key written by the Xamarin installer
			if (CheckRegistryKeyForExecutable (RegistryEx.CurrentUser, XAMARIN_ANDROID_INSTALLER_PATH, XAMARIN_ANDROID_INSTALLER_KEY, wow, "platform-tools", Adb))
				yield return RegistryEx.GetValueString (RegistryEx.CurrentUser, XAMARIN_ANDROID_INSTALLER_PATH, XAMARIN_ANDROID_INSTALLER_KEY, wow);

			// Check for the key written by the Android SDK installer
			foreach (var root in roots)
				if (CheckRegistryKeyForExecutable (root, ANDROID_INSTALLER_PATH, ANDROID_INSTALLER_KEY, wow, "platform-tools", Adb))
					yield return RegistryEx.GetValueString (root, ANDROID_INSTALLER_PATH, ANDROID_INSTALLER_KEY, wow);

			// Check some hardcoded paths for good measure
			var paths = new string [] {
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Xamarin", "MonoAndroid", "android-sdk-windows"),
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86), "Android", "android-sdk"),
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86), "Android", "android-sdk-windows"),
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFiles), "Android", "android-sdk"),
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Android", "android-sdk"),
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData), "Android", "android-sdk"),
				@"C:\android-sdk-windows"
			};
			foreach (var basePath in paths)
				if (Directory.Exists (basePath))
					if (ValidateAndroidSdkLocation (basePath))
						yield return basePath;
		}

		protected override string GetJavaSdkPath ()
		{
			// check the user specified path
			var roots = new[] { RegistryEx.CurrentUser, RegistryEx.LocalMachine };
			const RegistryEx.Wow64 wow = RegistryEx.Wow64.Key32;
			var regKey = GetMDRegistryKey ();

			foreach (var root in roots) {
				if (CheckRegistryKeyForExecutable (root, regKey, MDREG_JAVA_SDK, wow, "bin", JarSigner))
					return RegistryEx.GetValueString (root, regKey, MDREG_JAVA_SDK, wow);
			}

			string subkey = @"SOFTWARE\JavaSoft\Java Development Kit";

			Logger (TraceLevel.Info, "Looking for Java 6 SDK...");

			foreach (var wow64 in new[] { RegistryEx.Wow64.Key32, RegistryEx.Wow64.Key64 }) {
				string key_name = string.Format (@"{0}\{1}\{2}", "HKLM", subkey, "CurrentVersion");
				var currentVersion = RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey, "CurrentVersion", wow64);

				if (!string.IsNullOrEmpty (currentVersion)) {
					Logger (TraceLevel.Info, $"  Key {key_name} found.");

					// No matter what the CurrentVersion is, look for 1.6 or 1.7 or 1.8
					if (CheckRegistryKeyForExecutable (RegistryEx.LocalMachine, subkey + "\\" + "1.8", "JavaHome", wow64, "bin", JarSigner))
						return RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey + "\\" + "1.8", "JavaHome", wow64);

					if (CheckRegistryKeyForExecutable (RegistryEx.LocalMachine, subkey + "\\" + "1.7", "JavaHome", wow64, "bin", JarSigner))
						return RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey + "\\" + "1.7", "JavaHome", wow64);

					if (CheckRegistryKeyForExecutable (RegistryEx.LocalMachine, subkey + "\\" + "1.6", "JavaHome", wow64, "bin", JarSigner))
						return RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey + "\\" + "1.6", "JavaHome", wow64);
				}

				Logger (TraceLevel.Info, $"  Key {key_name} not found.");
			}

			// We ran out of things to check..
			return null;
		}

		protected override IEnumerable<string> GetAllAvailableAndroidNdks ()
		{
			var roots = new[] { RegistryEx.CurrentUser, RegistryEx.LocalMachine };
			var wow = RegistryEx.Wow64.Key32;
			var regKey = GetMDRegistryKey ();

			Logger (TraceLevel.Info, "Looking for Android NDK...");

			// Check for the key the user gave us in the VS/addin options
			foreach (var root in roots)
				if (CheckRegistryKeyForExecutable (root, regKey, MDREG_ANDROID_NDK, wow, ".", NdkStack))
					yield return RegistryEx.GetValueString (root, regKey, MDREG_ANDROID_NDK, wow);

			/*
			// Check for the key written by the Xamarin installer
			if (CheckRegistryKeyForExecutable (RegistryEx.CurrentUser, XAMARIN_ANDROID_INSTALLER_PATH, XAMARIN_ANDROID_INSTALLER_KEY, wow, "platform-tools", Adb))
				yield return RegistryEx.GetValueString (RegistryEx.CurrentUser, XAMARIN_ANDROID_INSTALLER_PATH, XAMARIN_ANDROID_INSTALLER_KEY, wow);
			*/

			// Check some hardcoded paths for good measure
			var xamarin_private = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Xamarin", "MonoAndroid");
			var vs_default = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData), "Microsoft", "AndroidNDK");
			var vs_default32bit = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData), "Microsoft", "AndroidNDK32");
			var vs_2017_default = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData), "Microsoft", "AndroidNDK64");
			var android_default = Path.Combine (OS.ProgramFilesX86, "Android");
			var cdrive_default = @"C:\";

			foreach (var basePath in new string [] {xamarin_private, android_default, vs_default, vs_default32bit, vs_2017_default, cdrive_default})
				if (Directory.Exists (basePath))
					foreach (var dir in Directory.GetDirectories (basePath, "android-ndk-r*"))
						if (ValidateAndroidNdkLocation (dir))
							yield return dir;
		}

		protected override string GetShortFormPath (string path)
		{
			return KernelEx.GetShortPathName (path);
		}

		public override void SetPreferredAndroidSdkPath (string path)
		{
			var regKey = GetMDRegistryKey ();
			RegistryEx.SetValueString (RegistryEx.CurrentUser, regKey, MDREG_ANDROID_SDK, path ?? "", RegistryEx.Wow64.Key32);
		}

		public override void SetPreferredJavaSdkPath (string path)
		{
			var regKey = GetMDRegistryKey ();
			RegistryEx.SetValueString (RegistryEx.CurrentUser, regKey, MDREG_JAVA_SDK, path ?? "", RegistryEx.Wow64.Key32);
		}

		public override void SetPreferredAndroidNdkPath (string path)
		{
			var regKey = GetMDRegistryKey ();
			RegistryEx.SetValueString (RegistryEx.CurrentUser, regKey, MDREG_ANDROID_NDK, path ?? "", RegistryEx.Wow64.Key32);
		}

		#region Helper Methods
		private bool CheckRegistryKeyForExecutable (UIntPtr key, string subkey, string valueName, RegistryEx.Wow64 wow64, string subdir, string exe)
		{
			string key_name = string.Format (@"{0}\{1}\{2}", key == RegistryEx.CurrentUser ? "HKCU" : "HKLM", subkey, valueName);

			var path = NullIfEmpty (RegistryEx.GetValueString (key, subkey, valueName, wow64));

			if (path == null) {
				Logger (TraceLevel.Info, $"  Key {key_name} not found.");
				return false;
			}

			if (!FindExecutableInDirectory (exe, Path.Combine (path, subdir)).Any ()) {
				Logger (TraceLevel.Info, $"  Key {key_name} found:\n    Path does not contain {exe} in \\{subdir} ({path}).");
				return false;
			}

			Logger (TraceLevel.Info, $"  Key {key_name} found:\n    Path contains {exe} in \\{subdir} ({path}).");

			return true;
		}
		#endregion
	}
}
