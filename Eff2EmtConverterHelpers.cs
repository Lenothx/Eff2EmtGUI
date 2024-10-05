using System.Collections.Generic;

namespace Eff2EmtGUI
{
    internal static class Eff2EmtConverterHelpers
    {
        public static readonly Dictionary<int, string> HardCodedSoundFiles = new Dictionary<int, string>()
        {
            {  39, "death_me" },
            { 143, "thunder1" },
            { 144, "thunder2" },
            { 158, "wind_lp1" },
            { 159, "rainloop" },
            { 160, "torch_lp" },
            { 161, "watundlp" }
        };

        public static readonly Dictionary<int, string> DefaultMusicFiles = new Dictionary<int, string>()
        {
            {  1, "bothunder.mp3" },
            {  2, "codecay.mp3" },
            {  3, "combattheme1.mp3" },
            {  4, "combattheme2.mp3" },
            {  5, "deaththeme.mp3" },
            {  6, "eqtheme.mp3" },
            {  7, "hohonor.mp3" },
            {  8, "poair.mp3" },
            {  9, "podisease.mp3" },
            { 10, "poearth.mp3" },
            { 11, "pofire.mp3" },
            { 12, "poinnovation.mp3" },
            { 13, "pojustice.mp3" },
            { 14, "poknowledge.mp3" },
            { 15, "ponightmare.mp3" },
            { 16, "postorms.mp3" },
            { 17, "potactics.mp3" },
            { 18, "potime.mp3" },
            { 19, "potorment.mp3" },
            { 20, "potranquility.mp3" },
            { 21, "povalor.mp3" },
            { 22, "powar.mp3" },
            { 23, "powater.mp3" },
            { 24, "solrotower.mp3" }
        };

        public static Dictionary<int, string> mp3indexFiles = null;

        public static List<string> SoundBank_Emit;
        public static List<string> SoundBank_Loop;

        public const string EMTLineFormat = ";?,SoundFile (wav=sound mp3/xmi=music),Unknown (0=OK 1=OK),WhenActive (0=Always 1=Daytime 2=Nighttime),Volume (1.0 = 100%),FadeInMS,FadeOutMS,WavLoopType (0=Constant 1=Delayed Repeat),X,Y,Z,WavFullVolRadius,WavMaxAudibleDist,NonZero = RandomizeLocation,ActivationRange,MinRepeatDelay,MaxRepeatDelay,xmiIndex,EchoLevel (50 = Max),IsEnvSound (for option toggle)";
    }
}