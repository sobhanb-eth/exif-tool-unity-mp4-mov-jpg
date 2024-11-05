using System;
using System.IO;
using UnityEngine;

namespace VideoLib
{
    public class VideoMetadataReader
    {
        public static VideoInfo ReadVideo(byte[] videoBytes, string name)
        {
            VideoInfo info = new VideoInfo();
            info.FileName = name;

            using (MemoryStream stream = new MemoryStream(videoBytes))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                try
                {
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        long position = reader.BaseStream.Position;
                        uint size = ReadUInt32(reader);
                        string type = ReadString(reader, 4);

                        if (size == 0)
                            break; // Invalid size

                        if (type == "moov")
                        {
                            // Process 'moov' atom
                            long moovEnd = position + size;
                            ProcessMoovAtom(reader, moovEnd, info);
                            break; // We've got what we need
                        }
                        else
                        {
                            // Skip this atom
                            reader.BaseStream.Seek(size - 8, SeekOrigin.Current);
                        }
                    }
                }
                catch (Exception e)
                {
                    // Handle exceptions (e.g., log error)
                    Console.WriteLine("Exception in ReadVideo: " + e.Message);
                }
            }

            // Adjust width and height based on rotation
            AdjustDimensionsBasedOnRotation(info);

            return info;
        }

        private static void AdjustDimensionsBasedOnRotation(VideoInfo info)
        {
            if (info.Rotation == 90 || info.Rotation == 270)
            {
                // Swap width and height
                int temp = info.Width;
                info.Width = info.Height;
                info.Height = temp;
            }
        }

        private static void ProcessMoovAtom(BinaryReader reader, long moovEnd, VideoInfo info)
        {
            while (reader.BaseStream.Position < moovEnd)
            {
                long position = reader.BaseStream.Position;
                uint size = ReadUInt32(reader);
                string type = ReadString(reader, 4);

                if (size == 0)
                    break; // Invalid size

                if (type == "trak")
                {
                    long trakEnd = position + size;
                    ProcessTrakAtom(reader, trakEnd, info);
                    if (info.Width > 0 && info.Height > 0)
                    {
                        // We found the video track and got the info we need
                        break;
                    }
                }
                else
                {
                    reader.BaseStream.Seek(size - 8, SeekOrigin.Current);
                }
            }
        }

        private static void ProcessTrakAtom(BinaryReader reader, long trakEnd, VideoInfo info)
        {
            long position = reader.BaseStream.Position;

            // We need to check if this trak is a video trak
            bool isVideoTrack = false;
            long tkhdPosition = -1;
            long tkhdSize = -1;

            while (reader.BaseStream.Position < trakEnd)
            {
                position = reader.BaseStream.Position;
                uint size = ReadUInt32(reader);
                string type = ReadString(reader, 4);

                if (size == 0)
                    break; // Invalid size

                if (type == "tkhd")
                {
                    // Store position to process later if this is a video track
                    tkhdPosition = position;
                    tkhdSize = size;
                    reader.BaseStream.Seek(size - 8, SeekOrigin.Current);
                }
                else if (type == "mdia")
                {
                    long mdiaEnd = position + size;
                    isVideoTrack = ProcessMdiaAtom(reader, mdiaEnd);
                    // Return to position after 'mdia'
                    reader.BaseStream.Seek(mdiaEnd, SeekOrigin.Begin);
                }
                else
                {
                    reader.BaseStream.Seek(size - 8, SeekOrigin.Current);
                }
            }

            if (isVideoTrack && tkhdPosition >= 0)
            {
                // Process 'tkhd' atom
                reader.BaseStream.Seek(tkhdPosition + 8, SeekOrigin.Begin); // Skip size and type
                ProcessTkhdAtom(reader, info);
            }
        }

        private static bool ProcessMdiaAtom(BinaryReader reader, long mdiaEnd)
        {
            while (reader.BaseStream.Position < mdiaEnd)
            {
                long position = reader.BaseStream.Position;
                uint size = ReadUInt32(reader);
                string type = ReadString(reader, 4);

                if (size == 0)
                    break; // Invalid size

                if (type == "hdlr")
                {
                    // Process 'hdlr' atom
                    string handlerType = ProcessHdlrAtom(reader);
                    if (handlerType == "vide")
                    {
                        return true; // This is a video track
                    }
                    else
                    {
                        return false; // Not a video track
                    }
                }
                else
                {
                    reader.BaseStream.Seek(size - 8, SeekOrigin.Current);
                }
            }
            return false; // Default to false if no 'hdlr' atom found
        }

        private static string ProcessHdlrAtom(BinaryReader reader)
        {
            // Skip version and flags (4 bytes)
            reader.ReadBytes(4);
            // Skip pre_defined (4 bytes)
            reader.ReadBytes(4);
            // Read handler_type (4 bytes)
            string handlerType = ReadString(reader, 4);
            // Skip the rest of the 'hdlr' atom
            return handlerType;
        }

        private static void ProcessTkhdAtom(BinaryReader reader, VideoInfo info)
        {
            // Similar to previous implementation, but with careful extraction of matrix and dimensions

            // Read version and flags
            byte version = reader.ReadByte();
            byte[] flags = reader.ReadBytes(3);

            if (version == 0)
            {
                // Skip unnecessary bytes as per version 0 structure
                reader.ReadBytes(16); // creation_time, modification_time, track_ID, reserved
                reader.ReadBytes(4);  // duration
            }
            else if (version == 1)
            {
                reader.ReadBytes(24); // creation_time, modification_time, track_ID, reserved
                reader.ReadBytes(8);  // duration
            }
            else
            {
                // Unknown version
                return;
            }

            // Common skipping
            reader.ReadBytes(8); // reserved
            reader.ReadBytes(4); // layer and alternate group
            reader.ReadBytes(4); // volume and reserved

            // Read transformation matrix
            byte[] matrixBytes = reader.ReadBytes(36);

            // Extract rotation from the matrix
            float rotation = GetRotationFromMatrix(matrixBytes);
            info.Rotation = rotation;

            // Read width and height (32-bit fixed-point numbers)
            uint widthFixed = ReadUInt32(reader);
            uint heightFixed = ReadUInt32(reader);

            info.Width = (int)(widthFixed >> 16);
            info.Height = (int)(heightFixed >> 16);
        }

        private static float GetRotationFromMatrix(byte[] matrix)
        {
            // The rotation is encoded in the matrix's first few elements
            // [ a, b, u ]
            // [ c, d, v ]
            // [ tx, ty, w ]

            // Read 'a', 'b', 'c', 'd' from the matrix
            double a = ReadFixedPoint(matrix, 0);
            double b = ReadFixedPoint(matrix, 4);
            double c = ReadFixedPoint(matrix, 8);
            double d = ReadFixedPoint(matrix, 12);

            // Calculate rotation in degrees
            double radians = Math.Atan2(b, a);
            float degrees = (float)(radians * (180.0 / Math.PI));

            // Ensure the rotation is between 0 and 360
            if (degrees < 0)
                degrees += 360;

            // Round the rotation to the nearest multiple of 90
            degrees = Mathf.Round(degrees / 90f) * 90f;

            return degrees;
        }


        private static double ReadFixedPoint(byte[] data, int offset)
        {
            // Fixed-point 16.16 number
            int whole = (data[offset] << 8) | data[offset + 1];
            int fraction = (data[offset + 2] << 8) | data[offset + 3];
            double value = whole + fraction / 65536.0;
            return value;
        }

        private static uint ReadUInt32(BinaryReader reader)
        {
            byte[] data = reader.ReadBytes(4);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(data);
            return BitConverter.ToUInt32(data, 0);
        }

        private static string ReadString(BinaryReader reader, int length)
        {
            byte[] data = reader.ReadBytes(length);
            return System.Text.Encoding.ASCII.GetString(data);
        }
    }
}
