using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Eff2EmtGUI
{
    internal class Eff2EmtConverter
    {
        protected struct EffSoundEntry
        {
            public Int32 UnkRef00;
            public Int32 UnkRef04;
            public Int32 Reserved;
            public Int32 Sequence;
            public float X;
            public float Y;
            public float Z;
            public float Radius;
            public Int32 Cooldown1;
            public Int32 Cooldown2;
            public Int32 RandomDelay;
            public Int32 Unk44;
            public Int32 SoundID1;
            public Int32 SoundID2;
            public Byte SoundType;
            public Byte UnkPad57;
            public Byte UnkPad58;
            public Byte UnkPad59;
            public Int32 AsDistance;
            public Int32 UnkRange64;
            public Int32 FadeOutMS;
            public Int32 UnkRange72;
            public Int32 FullVolRange;
            public Int32 UnkRange80;
        };

        protected class EmtSoundEntry
        {
            public string EntryType = "2";
            public string SoundFile = "";
            public Int32 Reserved1 = 0;
            public Int32 WhenActive = 0;
            public float Volume = 1.0f;
            public Int32 FadeInMS = 500;
            public Int32 FadeOutMS = 1000;
            public Int32 WavLoopType = 0;
            public float X, Y, Z;
            public float WavFullVolRadius = 50;
            public float WavMaxAudibleDist = 50;
            public bool RandomizeLocation = false;
            public Int32 ActivationRange = 50;
            public Int32 MinRepeatDelay = 0;
            public Int32 MaxRepeatDelay = 0;
            public Int32 xmiIndex = 0;
            public Int32 EchoLevel = 0;
            public bool IsEnvSound = true;

            // ?,SoundFile (wav=sound mp3/xmi=music),Unknown (0=OK 1=OK),WhenActive (0=Always 1=Daytime 2=Nighttime),Volume (1.0 = 100%),FadeInMS,FadeOutMS,WavLoopType (0=Constant 1=Delayed Repeat),X,Y,Z,WavFullVolRadius,WavMaxAudibleDist,NonZero = RandomizeLocation,ActivationRange,MinRepeatDelay,MaxRepeatDelay,xmiIndex,EchoLevel (50 = Max),IsEnvSound (for option toggle)
            public bool IsValid
            {
                get
                {
                    return ((SoundFile != null) && (SoundFile.Length > 0));
                }
            }

            public EmtSoundEntry Clone()
            {
                EmtSoundEntry _new = new EmtSoundEntry
                {
                    EntryType = this.EntryType,
                    SoundFile = this.SoundFile,
                    Reserved1 = this.Reserved1,
                    WhenActive = this.WhenActive,
                    Volume = this.Volume,
                    FadeInMS = this.FadeInMS,
                    FadeOutMS = this.FadeOutMS,
                    WavLoopType = this.WavLoopType,
                    X = this.X,
                    Y = this.Y,
                    Z = this.Z,
                    WavFullVolRadius = this.WavFullVolRadius,
                    WavMaxAudibleDist = this.WavMaxAudibleDist,
                    RandomizeLocation = this.RandomizeLocation,
                    ActivationRange = this.ActivationRange,
                    MinRepeatDelay = this.MinRepeatDelay,
                    MaxRepeatDelay = this.MaxRepeatDelay,
                    xmiIndex = this.xmiIndex,
                    EchoLevel = this.EchoLevel,
                    IsEnvSound = this.IsEnvSound
                };

                return _new;
            }

            public override string ToString()
            {
                if (!IsValid)
                {
                    return string.Empty;
                }

                var fields = new object[]
                {
        EntryType,
        SoundFile,
        Reserved1,
        WhenActive,
        Volume.ToString("F1"),
        FadeInMS,
        FadeOutMS,
        WavLoopType,
        X.ToString("F1"),
        Y.ToString("F1"),
        Z.ToString("F1"),
        WavFullVolRadius.ToString("F1"),
        WavMaxAudibleDist.ToString("F1"),
        RandomizeLocation ? "1" : "0",
        ActivationRange,
        MinRepeatDelay,
        MaxRepeatDelay,
        xmiIndex,
        EchoLevel,
        IsEnvSound ? "1" : "0"
                };

                // Convert all fields to strings and join them with commas.
                return string.Join(",", fields.Select(field => field?.ToString() ?? string.Empty));
            }
        }

        // Return the entry from mp3index.txt on line number abs(ID)
        protected static string Mp3indexFile(int ID) => Eff2EmtConverterHelpers.mp3indexFiles?.TryGetValue(Math.Abs(ID), out var result) == true ? result : "";

        // Convert SoundID from ZoneNick_sounds.eff into a sound file name
        protected static string SoundFileNumber(int soundID)
        {
            if (soundID == 0)
                return ""; // No sound

            if (soundID < 0)
                return Mp3indexFile(soundID); // Music File Reference

            if (soundID < 32)
                return GetSoundFromList(Eff2EmtConverterHelpers.SoundBank_Emit, soundID - 1); // EMIT Section Sound Reference

            if (soundID > 161)
                return GetSoundFromList(Eff2EmtConverterHelpers.SoundBank_Loop, soundID - 162); // LOOP Section Sound Reference

            // Hard-Coded Sound Files
            return Eff2EmtConverterHelpers.HardCodedSoundFiles.TryGetValue(soundID, out var result) ? result : "";
        }

        private static string GetSoundFromList(IList<string> soundList, int index) =>
            soundList != null && index >= 0 && index < soundList.Count ? soundList[index] : "";

        public static DialogResult ConvertZone(string EQFolder, string ZoneNick)
        {
            if (!ValidateInputs(EQFolder, ZoneNick))
            {
                return DialogResult.Abort;
            }

            string _zoneSoundEntriesFilename = Path.Combine(EQFolder, $"{ZoneNick}_sounds.eff");
            string _zoneSoundBankFilename = Path.Combine(EQFolder, $"{ZoneNick}_sndbnk.eff");
            string _zoneSoundEmitterFilename = Path.Combine(EQFolder, $"{ZoneNick}.emt");

            InitializeMp3IndexFiles(EQFolder);

            // Step 1: Read ZoneNick_sounds.eff (Required)
            BinaryReader _effFile = OpenBinaryFileWithRetry(_zoneSoundEntriesFilename, "Sound Entries");

            if (_effFile == null)
                return DialogResult.Abort;

            // Step 2: Read existing .emt file contents for merging
            List<string> _emtEntries = ReadEmitterFile(_zoneSoundEmitterFilename);

            // Step 3: Open or create ZoneNick.emt for writing
            StreamWriter _emtFile = OpenTextFileWithRetry(_zoneSoundEmitterFilename, "Sound Emitter");
            if (_emtFile == null)
                return DialogResult.Abort;

            // Step 4: Read sound bank file and categorize sounds
            ReadSoundBankFile(_zoneSoundBankFilename);

            // Step 5: Initialize the .emt file with header if needed
            WriteEmtHeader(_emtEntries, _emtFile);

            // Step 6: Process and convert binary entries into text entries
            ConvertEffEntriesToEmt(_effFile, _emtEntries, _emtFile, ZoneNick);

            _emtFile.Close();
            return DialogResult.OK;
        }

        // Validate folder and zone nick inputs
        private static bool ValidateInputs(string EQFolder, string ZoneNick)
        {
            return !string.IsNullOrEmpty(EQFolder) && !string.IsNullOrEmpty(ZoneNick);
        }

        // Initialize or read the mp3index files
        private static void InitializeMp3IndexFiles(string EQFolder)
        {
            if (Eff2EmtConverterHelpers.mp3indexFiles != null) return;

            string mp3IndexPath = Path.Combine(EQFolder, "mp3index.txt");

            if (File.Exists(mp3IndexPath))
            {
                Eff2EmtConverterHelpers.mp3indexFiles = new Dictionary<int, string>();
                int lineNumber = 1;

                foreach (var line in File.ReadAllLines(mp3IndexPath))
                {
                    Eff2EmtConverterHelpers.mp3indexFiles[lineNumber++] = line;
                }
            }
            else
            {
                Eff2EmtConverterHelpers.mp3indexFiles = Eff2EmtConverterHelpers.DefaultMusicFiles;
                MessageBox.Show($"Note: Could not find the mp3index file at {mp3IndexPath}. Using default values from Live.",
                                "No mp3index.txt - Using Defaults", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // Open a binary file with retry logic
        private static BinaryReader OpenBinaryFileWithRetry(string filename, string fileType)
        {
            BinaryReader file = null;
            while (file == null)
            {
                try
                {
                    file = new BinaryReader(File.OpenRead(filename));
                }
                catch (Exception ex)
                {
                    var result = MessageBox.Show($"Could not create {fileType} File:\n\n{filename}\n\nError Message:\n\n{ex.Message}",
                                                 $"{fileType} Read Error", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Error);

                    if (result == DialogResult.Ignore)
                        return null;
                    if (result == DialogResult.Abort)
                        return null;
                }
            }
            return file;
        }

        // Read the .emt emitter file
        private static List<string> ReadEmitterFile(string filename)
        {
            var entries = new List<string>();
            if (File.Exists(filename))
            {
                entries.AddRange(File.ReadLines(filename));
            }
            return entries;
        }

        // Open a text file with retry logic
        private static StreamWriter OpenTextFileWithRetry(string filename, string fileType)
        {
            StreamWriter file = null;
            while (file == null)
            {
                try
                {
                    file = new StreamWriter(filename);
                }
                catch (Exception ex)
                {
                    var result = MessageBox.Show($"Could not create {fileType} File:\n\n{filename}\n\nError Message:\n\n{ex.Message}",
                                                 $"{fileType} Creation Error", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Error);

                    if (result == DialogResult.Ignore)
                        return null;
                    if (result == DialogResult.Abort)
                        return null;
                }
            }
            return file;
        }

        // Read sound bank file and categorize sounds into emit and loop sections
        private static void ReadSoundBankFile(string filename)
        {
            Eff2EmtConverterHelpers.SoundBank_Emit = new List<string>();
            Eff2EmtConverterHelpers.SoundBank_Loop = new List<string>();

            if (!File.Exists(filename))
            {
                Console.WriteLine($"Sound bank file {filename} not found.");
                return;
            }

            bool inEmitSection = true;

            foreach (var line in File.ReadLines(filename))
            {
                var trimmedLine = line.Trim();

                switch (trimmedLine)
                {
                    case "EMIT":
                        inEmitSection = true;
                        break;

                    case "LOOP":
                    case "RAND":
                        inEmitSection = false;
                        break;

                    default:
                        if (!string.IsNullOrWhiteSpace(trimmedLine))
                        {
                            if (inEmitSection)
                            {
                                Eff2EmtConverterHelpers.SoundBank_Emit.Add(trimmedLine);
                            }
                            else
                            {
                                Eff2EmtConverterHelpers.SoundBank_Loop.Add(trimmedLine);
                            }
                        }
                        break;
                }
            }
        }

        // Write the EMT header line if necessary
        private static void WriteEmtHeader(List<string> emtEntries, StreamWriter emtFile)
        {
            if (emtEntries.Count == 0 || emtEntries[0].Length < 1 || emtEntries[0][0] != ';')
            {
                emtFile.WriteLine(Eff2EmtConverterHelpers.EMTLineFormat);
            }
        }

        // Convert binary sound entries into .emt text entries
        private static void ConvertEffEntriesToEmt(BinaryReader effFile, List<string> emtEntries, StreamWriter emtFile, string zoneNick)
        {
            while ((effFile.BaseStream.Length - effFile.BaseStream.Position) >= 84)
            {
                EffSoundEntry effEntry = ReadEffEntry(effFile);

                EmtSoundEntry sound1 = CreateEmtSoundEntry(effEntry, zoneNick, out EmtSoundEntry sound2);

                WriteSoundEntryToFile(sound1, emtEntries, emtFile);
                WriteSoundEntryToFile(sound2, emtEntries, emtFile);
            }
        }

        // Read a single EffSoundEntry from the binary reader
        private static EffSoundEntry ReadEffEntry(BinaryReader effFile)
        {
            return new EffSoundEntry
            {
                UnkRef00 = effFile.ReadInt32(),
                UnkRef04 = effFile.ReadInt32(),
                Reserved = effFile.ReadInt32(),
                Sequence = effFile.ReadInt32(),
                X = effFile.ReadSingle(),
                Y = effFile.ReadSingle(),
                Z = effFile.ReadSingle(),
                Radius = effFile.ReadSingle(),
                Cooldown1 = effFile.ReadInt32(),
                Cooldown2 = effFile.ReadInt32(),
                RandomDelay = effFile.ReadInt32(),
                Unk44 = effFile.ReadInt32(),
                SoundID1 = effFile.ReadInt32(),
                SoundID2 = effFile.ReadInt32(),
                SoundType = effFile.ReadByte(),
                UnkPad57 = effFile.ReadByte(),
                UnkPad58 = effFile.ReadByte(),
                UnkPad59 = effFile.ReadByte(),
                AsDistance = effFile.ReadInt32(),
                UnkRange64 = effFile.ReadInt32(),
                FadeOutMS = effFile.ReadInt32(),
                UnkRange72 = effFile.ReadInt32(),
                FullVolRange = effFile.ReadInt32(),
                UnkRange80 = effFile.ReadInt32()
            };
        }

        // Create and configure EmtSoundEntry objects based on the EffSoundEntry
        private static EmtSoundEntry CreateEmtSoundEntry(EffSoundEntry effEntry, string zoneNick, out EmtSoundEntry sound2)
        {
            // Initialize the primary sound entry based on effEntry data
            var sound1 = new EmtSoundEntry
            {
                SoundFile = GetSoundFile(effEntry.SoundID1, zoneNick),
                WhenActive = 1,
                FadeOutMS = Math.Max(effEntry.FadeOutMS, 100),
                FadeInMS = GetFadeInMS(effEntry.FadeOutMS),
                X = effEntry.X,
                Y = effEntry.Y,
                Z = effEntry.Z,
                Volume = GetVolumeByDistance(effEntry.AsDistance, effEntry.SoundType),
                WavFullVolRadius = (effEntry.SoundType == 0) ? effEntry.Radius : Math.Max(effEntry.FullVolRange, 0),
                ActivationRange = (int)effEntry.Radius,
                WavMaxAudibleDist = effEntry.Radius, // Set max audible distance to activation range if no other mapping available
                IsEnvSound = (effEntry.SoundType != 1), // Music files ignore cooldowns in ZoneNick.emt.
                xmiIndex = (effEntry.SoundType == 1 && effEntry.SoundID1 > 0 && effEntry.SoundID1 < 32) ? effEntry.SoundID1 : 0
            };

            // Apply loop type and delay settings if it's not a music file
            if (effEntry.SoundType != 1)
            {
                SetLoopTypeAndDelay(sound1, effEntry.Cooldown1, effEntry.RandomDelay);
            }

            // Set the secondary sound entry based on `SoundID2` and make necessary adjustments
            sound2 = null;
            string soundFile2 = GetSoundFile(effEntry.SoundID2, zoneNick);

            if (!string.IsNullOrEmpty(soundFile2))
            {
                // Create a copy of sound1 and modify it as needed for sound2
                sound2 = sound1.Clone();
                sound2.SoundFile = soundFile2;

                // Apply loop type and delay settings for sound2
                if (effEntry.SoundType != 1)
                {
                    SetLoopTypeAndDelay(sound2, effEntry.Cooldown2, effEntry.RandomDelay);
                }

                // Adjust `WhenActive` and `xmiIndex` depending on sound type
                switch (effEntry.SoundType)
                {
                    case 0: // Day/Night Sound Effect, Constant Volume
                        sound2.WhenActive = 2;
                        break;

                    case 1: // Background Music
                        sound2.WhenActive = 2;
                        sound2.xmiIndex = (effEntry.SoundID2 > 0 && effEntry.SoundID2 < 32) ? effEntry.SoundID2 : 0;
                        break;

                    case 3: // Day/Night Sound Effect, Volume by Distance
                        sound2.WhenActive = 2;
                        break;
                }
            }

            // Ensure the primary sound file name has the correct file extension
            if (!string.IsNullOrEmpty(sound1.SoundFile) && !sound1.SoundFile.Contains("."))
            {
                sound1.SoundFile += ".wav";
            }

            // Ensure the secondary sound file name has the correct file extension
            if (sound2 != null && !sound2.SoundFile.Contains("."))
            {
                sound2.SoundFile += ".wav";
            }

            return sound1;
        }

        // Helper methods used within CreateEmtSoundEntry

        private static string GetSoundFile(int soundID, string zoneNick)
        {
            string soundFile = SoundFileNumber(soundID);
            if (soundID > 0 && string.IsNullOrEmpty(soundFile))
            {
                soundFile = $"{zoneNick}.xmi";
            }

            return soundFile.Contains('.') ? soundFile : $"{soundFile}.wav";
        }

        private static float GetVolumeByDistance(int distance, int soundType)
        {
            // Check if the soundType should use volume by distance calculation
            if (soundType == 2 || soundType == 3)
            {
                // Return 0.0f if distance is out of range, otherwise calculate the volume
                if (distance < 0 || distance > 3000)
                {
                    return 0.0f;
                }
                return (3000.0f - distance) / 3000.0f;
            }

            // Default volume for other sound types
            return 1.0f;
        }

        private static int GetFadeInMS(int fadeOutMS) => Math.Min(fadeOutMS / 2, 5000);

        private static void SetLoopTypeAndDelay(EmtSoundEntry sound, int cooldown, int randomDelay)
        {
            sound.WavLoopType = (cooldown <= 0 && randomDelay <= 0) ? 0 : 1;
            sound.MinRepeatDelay = sound.WavLoopType == 0 ? 0 : Math.Max(cooldown, 0);
            sound.MaxRepeatDelay = sound.MinRepeatDelay + Math.Max(randomDelay, 0);
        }

        // Write a single EmtSoundEntry to the .emt file if it's not already present
        private static void WriteSoundEntryToFile(EmtSoundEntry soundEntry, List<string> emtEntries, StreamWriter emtFile)
        {
            if (soundEntry != null && !emtEntries.Contains(soundEntry.Clone().ToString()))
            {
                emtFile.WriteLine(soundEntry.Clone().ToString());
            }
        }
    }
}