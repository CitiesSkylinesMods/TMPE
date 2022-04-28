namespace TrafficManager.UI.Textures {
    using CSUtil.Commons;
    using System.IO;
    using System.Reflection;
    using System;
    using TrafficManager.State.ConfigData;
    using TrafficManager.Util;
    using UnityEngine;

    public static class TextureResources {
        internal static Texture2D LoadDllResource(string resourceName,
                                                  IntVector2 size,
                                                  bool mip = false,
                                                  bool logIfNotFound = true) {
#if DEBUG
            bool debug = DebugSwitch.ResourceLoading.Get();
#endif
            try {
#if DEBUG
                if (debug) {
                    Log._Debug($"Loading DllResource {resourceName}");
                }
#endif
                var myAssembly = Assembly.GetExecutingAssembly();
                var myStream =
                    myAssembly.GetManifestResourceStream(
                        "TrafficManager.Resources." + resourceName);
                if (myStream == null) {
                    if (logIfNotFound) {
                        throw new Exception($"Resource stream {resourceName} not found!");
                    }

                    Log._Debug($"Resource {resourceName} not found (not an error)");
                    return null;
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
                if (logIfNotFound) {
#if DEBUG
                    Log.Error($"Failed to load texture {resourceName}: " + e);
#else
                    Log.Warning($"Failed to load texture {resourceName}: " + e);
#endif
                }
                // If not found, silently return null
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