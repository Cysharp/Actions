// Generate a dummy Unity package file with some bytes.
var f = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 };
File.WriteAllBytes($"{Environment.CurrentDirectory}/Sandbox.Unity.unitypackage", f); // 0bytes file not allowed to upload to GitHub Release
File.WriteAllBytes($"{Environment.CurrentDirectory}/Sandbox.Unity.Plugin.unitypackage", f); // 0bytes file not allowed to upload to GitHub Release
