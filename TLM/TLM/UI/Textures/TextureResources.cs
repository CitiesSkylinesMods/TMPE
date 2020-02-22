namespace TrafficManager.UI.Textures {
    using CSUtil.Commons;
    using System.IO;
    using System.Reflection;
    using System;
    using TrafficManager.State.ConfigData;
    using UnityEngine;

    public static class TextureResources {
        static TextureResources() {
        }

        internal static Texture2D LoadDllResource(string resourceName, int width, int height)
        {
#if DEBUG
            bool debug = DebugSwitch.JunctionRestrictions.Get();
#endif
            try {
#if DEBUG
                if (debug) {
                    Log._Debug($"Loading DllResource {resourceName}");
                }
#endif
                var myAssembly = Assembly.GetExecutingAssembly();
                var myStream = myAssembly.GetManifestResourceStream("TrafficManager.Resources." + resourceName);

                var texture = new Texture2D(width, height, TextureFormat.ARGB32, false);

                texture.LoadImage(ReadToEnd(myStream));

                return texture;
            } catch (Exception e) {
                Log.Error(e.StackTrace.ToString());
                return null;
            }
        }

        static byte[] ReadToEnd(Stream stream)
        {
            var originalPosition = stream.Position;
            stream.Position = 0;

            try
            {
                var readBuffer = new byte[4096];

                var totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
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
            finally
            {
                stream.Position = originalPosition;
            }
        }
    }
}