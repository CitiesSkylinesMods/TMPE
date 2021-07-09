namespace TrafficManager.UI.Textures {
    using CSUtil.Commons;
    using System.IO;
    using System.Reflection;
    using System;
    using TrafficManager.State;
    using TrafficManager.State.ConfigData;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>
    /// Provides resource loading utility functions.
    /// </summary>
    public static class TextureResources {
        /// <summary>Loads a texture from DLL resource.</summary>
        /// <param name="resourceName">Path to resource inside Resources/ directory separated by "."</param>
        /// <param name="size">Expected texture size.</param>
        /// <param name="mip">Whether mip levels are to be created and used.</param>
        /// <returns>New texture.</returns>
        /// <exception cref="Exception">Loading failed.</exception>
        internal static Texture2D LoadDllResource(string resourceName,
                                                  IntVector2 size,
                                                  bool mip = false) {
            bool debugResourceLoading = GlobalConfig.Instance.Debug.ResourceLoading;
            try {
                if (debugResourceLoading) {
                    Log._Debug($"Loading DllResource {resourceName}");
                }

                var myAssembly = Assembly.GetExecutingAssembly();
                var myStream = myAssembly.GetManifestResourceStream("TrafficManager.Resources." + resourceName);
                if (myStream == null) {
                    if (debugResourceLoading) {
                        Log._DebugOnlyError($"Resource not found: {resourceName}");
                    }
                    throw new Exception($"Resource stream {resourceName} not found!");
                }

                var texture = new Texture2D(
                    width: size.x,
                    height: size.y,
                    format: TextureFormat.ARGB32,
                    mipmap: mip);

                texture.LoadImage(ReadToEnd(myStream));

                return texture;
            }
            catch (Exception e) {
#if DEBUG
                Log.Error("Failed to load " + e);
#else
                Log.Warning("Failed to load " + e);
#endif
                return null;
            }
        }

        static byte[] ReadToEnd(Stream stream) {
            var originalPosition = stream.Position;
            stream.Position = 0;

            try {
                var readBuffer = new byte[4096];

                var totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(
                            readBuffer,
                            totalBytesRead,
                            readBuffer.Length - totalBytesRead)) > 0) {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead != readBuffer.Length)
                        continue;

                    var nextByte = stream.ReadByte();
                    if (nextByte == -1)
                        continue;

                    var temp = new byte[readBuffer.Length * 2];
                    Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                    Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                    readBuffer = temp;
                    totalBytesRead++;
                }

                var buffer = readBuffer;
                if (readBuffer.Length == totalBytesRead)
                    return buffer;

                buffer = new byte[totalBytesRead];
                Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                return buffer;
            }
            catch (Exception e) {
                Log.Error(e.StackTrace.ToString());
                return null;
            }
            finally {
                stream.Position = originalPosition;
            }
        }
    }
}