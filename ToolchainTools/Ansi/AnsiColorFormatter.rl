%%{
    machine tp;

	variable p inputIndex;
	variable pe inputLength;
	variable eof atEnd;
	variable data inputText;
    variable cs currentState;

    hexColor = '#' xdigit{6};

    main :=
    (
          hexColor @{ color = Color.FromHex(inputText[..(inputIndex + 1)]); return true; }
        | "black" @{ color = Color.Black; return true; }
        | "maroon" @{ color = Color.Maroon; return true; }
        | "green" @{ color = Color.Green; return true; }
        | "olive" @{ color = Color.Olive; return true; }
        | "navy" @{ color = Color.Navy; return true; }
        | "purple" @{ color = Color.Purple; return true; }
        | "teal" @{ color = Color.Teal; return true; }
        | "silver" @{ color = Color.Silver; return true; }
        | "gray" @{ color = Color.Gray; return true; }
        | "red" @{ color = Color.Red; return true; }
        | "lime" @{ color = Color.Lime; return true; }
        | "yellow" @{ color = Color.Yellow; return true; }
        | "blue" @{ color = Color.Blue; return true; }
        | "fuchsia" @{ color = Color.Fuchsia; return true; }
        | "magenta" @{ color = Color.Magenta1; return true; }
        | "aqua" @{ color = Color.Aqua; return true; }
        | "cyan" @{ color = Color.Cyan1; return true; }
        | "white" @{ color = Color.White; return true; }
        | "gray0" @{ color = Color.Gray0; return true; }
        | "navyblue" @{ color = Color.NavyBlue; return true; }
        | "darkblue" @{ color = Color.DarkBlue; return true; }
        | "blue3" @{ color = Color.Blue3; return true; }
        | "blue3_1" @{ color = Color.Blue3_1; return true; }
        | "blue1" @{ color = Color.Blue1; return true; }
        | "darkgreen" @{ color = Color.DarkGreen; return true; }
        | "deepskyblue4" @{ color = Color.DeepSkyBlue4; return true; }
        | "deepskyblue4_1" @{ color = Color.DeepSkyBlue4_1; return true; }
        | "deepskyblue4_2" @{ color = Color.DeepSkyBlue4_2; return true; }
        | "dodgerblue3" @{ color = Color.DodgerBlue3; return true; }
        | "dodgerblue2" @{ color = Color.DodgerBlue2; return true; }
        | "green4" @{ color = Color.Green4; return true; }
        | "springgreen4" @{ color = Color.SpringGreen4; return true; }
        | "turquoise4" @{ color = Color.Turquoise4; return true; }
        | "deepskyblue3" @{ color = Color.DeepSkyBlue3; return true; }
        | "deepskyblue3_1" @{ color = Color.DeepSkyBlue3_1; return true; }
        | "dodgerblue1" @{ color = Color.DodgerBlue1; return true; }
        | "green3" @{ color = Color.Green3; return true; }
        | "springgreen3" @{ color = Color.SpringGreen3; return true; }
        | "darkcyan" @{ color = Color.DarkCyan; return true; }
        | "lightseagreen" @{ color = Color.LightSeaGreen; return true; }
        | "deepskyblue2" @{ color = Color.DeepSkyBlue2; return true; }
        | "deepskyblue1" @{ color = Color.DeepSkyBlue1; return true; }
        | "green3_1" @{ color = Color.Green3_1; return true; }
        | "springgreen3_1" @{ color = Color.SpringGreen3_1; return true; }
        | "springgreen2" @{ color = Color.SpringGreen2; return true; }
        | "cyan3" @{ color = Color.Cyan3; return true; }
        | "darkturquoise" @{ color = Color.DarkTurquoise; return true; }
        | "turquoise2" @{ color = Color.Turquoise2; return true; }
        | "green1" @{ color = Color.Green1; return true; }
        | "springgreen2_1" @{ color = Color.SpringGreen2_1; return true; }
        | "springgreen1" @{ color = Color.SpringGreen1; return true; }
        | "mediumspringgreen" @{ color = Color.MediumSpringGreen; return true; }
        | "cyan2" @{ color = Color.Cyan2; return true; }
        | "cyan1" @{ color = Color.Cyan1; return true; }
        | "darkred" @{ color = Color.DarkRed; return true; }
        | "deeppink4" @{ color = Color.DeepPink4; return true; }
        | "purple4" @{ color = Color.Purple4; return true; }
        | "purple4_1" @{ color = Color.Purple4_1; return true; }
        | "purple3" @{ color = Color.Purple3; return true; }
        | "blueviolet" @{ color = Color.BlueViolet; return true; }
        | "orange4" @{ color = Color.Orange4; return true; }
        | "gray37" @{ color = Color.Gray37; return true; }
        | "mediumpurple4" @{ color = Color.MediumPurple4; return true; }
        | "slateblue3" @{ color = Color.SlateBlue3; return true; }
        | "slateblue3_1" @{ color = Color.SlateBlue3_1; return true; }
        | "royalblue1" @{ color = Color.RoyalBlue1; return true; }
        | "chartreuse4" @{ color = Color.Chartreuse4; return true; }
        | "darkseagreen4" @{ color = Color.DarkSeaGreen4; return true; }
        | "paleturquoise4" @{ color = Color.PaleTurquoise4; return true; }
        | "steelblue" @{ color = Color.SteelBlue; return true; }
        | "steelblue3" @{ color = Color.SteelBlue3; return true; }
        | "cornflowerblue" @{ color = Color.CornflowerBlue; return true; }
        | "chartreuse3" @{ color = Color.Chartreuse3; return true; }
        | "darkseagreen4_1" @{ color = Color.DarkSeaGreen4_1; return true; }
        | "cadetblue" @{ color = Color.CadetBlue; return true; }
        | "cadetblue_1" @{ color = Color.CadetBlue_1; return true; }
        | "skyblue3" @{ color = Color.SkyBlue3; return true; }
        | "steelblue1" @{ color = Color.SteelBlue1; return true; }
        | "chartreuse3_1" @{ color = Color.Chartreuse3_1; return true; }
        | "palegreen3" @{ color = Color.PaleGreen3; return true; }
        | "seagreen3" @{ color = Color.SeaGreen3; return true; }
        | "aquamarine3" @{ color = Color.Aquamarine3; return true; }
        | "mediumturquoise" @{ color = Color.MediumTurquoise; return true; }
        | "steelblue1_1" @{ color = Color.SteelBlue1_1; return true; }
        | "chartreuse2" @{ color = Color.Chartreuse2; return true; }
        | "seagreen2" @{ color = Color.SeaGreen2; return true; }
        | "seagreen1" @{ color = Color.SeaGreen1; return true; }
        | "seagreen1_1" @{ color = Color.SeaGreen1_1; return true; }
        | "aquamarine1" @{ color = Color.Aquamarine1; return true; }
        | "darkslategray2" @{ color = Color.DarkSlateGray2; return true; }
        | "darkred_1" @{ color = Color.DarkRed_1; return true; }
        | "deeppink4_1" @{ color = Color.DeepPink4_1; return true; }
        | "darkmagenta" @{ color = Color.DarkMagenta; return true; }
        | "darkmagenta_1" @{ color = Color.DarkMagenta_1; return true; }
        | "darkviolet" @{ color = Color.DarkViolet; return true; }
        | "purple_1" @{ color = Color.Purple_1; return true; }
        | "orange4_1" @{ color = Color.Orange4_1; return true; }
        | "lightpink4" @{ color = Color.LightPink4; return true; }
        | "plum4" @{ color = Color.Plum4; return true; }
        | "mediumpurple3" @{ color = Color.MediumPurple3; return true; }
        | "mediumpurple3_1" @{ color = Color.MediumPurple3_1; return true; }
        | "slateblue1" @{ color = Color.SlateBlue1; return true; }
        | "yellow4" @{ color = Color.Yellow4; return true; }
        | "wheat4" @{ color = Color.Wheat4; return true; }
        | "gray53" @{ color = Color.Gray53; return true; }
        | "lightslategray" @{ color = Color.LightSlateGray; return true; }
        | "mediumpurple" @{ color = Color.MediumPurple; return true; }
        | "lightslateblue" @{ color = Color.LightSlateBlue; return true; }
        | "yellow4_1" @{ color = Color.Yellow4_1; return true; }
        | "darkolivegreen3" @{ color = Color.DarkOliveGreen3; return true; }
        | "darkseagreen" @{ color = Color.DarkSeaGreen; return true; }
        | "lightskyblue3" @{ color = Color.LightSkyBlue3; return true; }
        | "lightskyblue3_1" @{ color = Color.LightSkyBlue3_1; return true; }
        | "skyblue2" @{ color = Color.SkyBlue2; return true; }
        | "chartreuse2_1" @{ color = Color.Chartreuse2_1; return true; }
        | "darkolivegreen3_1" @{ color = Color.DarkOliveGreen3_1; return true; }
        | "palegreen3_1" @{ color = Color.PaleGreen3_1; return true; }
        | "darkseagreen3" @{ color = Color.DarkSeaGreen3; return true; }
        | "darkslategray3" @{ color = Color.DarkSlateGray3; return true; }
        | "skyblue1" @{ color = Color.SkyBlue1; return true; }
        | "chartreuse1" @{ color = Color.Chartreuse1; return true; }
        | "lightgreen" @{ color = Color.LightGreen; return true; }
        | "lightgreen_1" @{ color = Color.LightGreen_1; return true; }
        | "palegreen1" @{ color = Color.PaleGreen1; return true; }
        | "aquamarine1_1" @{ color = Color.Aquamarine1_1; return true; }
        | "darkslategray1" @{ color = Color.DarkSlateGray1; return true; }
        | "red3" @{ color = Color.Red3; return true; }
        | "deeppink4_2" @{ color = Color.DeepPink4_2; return true; }
        | "mediumvioletred" @{ color = Color.MediumVioletRed; return true; }
        | "magenta3" @{ color = Color.Magenta3; return true; }
        | "darkviolet_1" @{ color = Color.DarkViolet_1; return true; }
        | "purple_2" @{ color = Color.Purple_2; return true; }
        | "darkorange3" @{ color = Color.DarkOrange3; return true; }
        | "indianred" @{ color = Color.IndianRed; return true; }
        | "hotpink3" @{ color = Color.HotPink3; return true; }
        | "mediumorchid3" @{ color = Color.MediumOrchid3; return true; }
        | "mediumorchid" @{ color = Color.MediumOrchid; return true; }
        | "mediumpurple2" @{ color = Color.MediumPurple2; return true; }
        | "darkgoldenrod" @{ color = Color.DarkGoldenrod; return true; }
        | "lightsalmon3" @{ color = Color.LightSalmon3; return true; }
        | "rosybrown" @{ color = Color.RosyBrown; return true; }
        | "gray63" @{ color = Color.Gray63; return true; }
        | "mediumpurple2_1" @{ color = Color.MediumPurple2_1; return true; }
        | "mediumpurple1" @{ color = Color.MediumPurple1; return true; }
        | "gold3" @{ color = Color.Gold3; return true; }
        | "darkkhaki" @{ color = Color.DarkKhaki; return true; }
        | "navajowhite3" @{ color = Color.NavajoWhite3; return true; }
        | "gray69" @{ color = Color.Gray69; return true; }
        | "lightsteelblue3" @{ color = Color.LightSteelBlue3; return true; }
        | "lightsteelblue" @{ color = Color.LightSteelBlue; return true; }
        | "yellow3" @{ color = Color.Yellow3; return true; }
        | "darkolivegreen3_2" @{ color = Color.DarkOliveGreen3_2; return true; }
        | "darkseagreen3_1" @{ color = Color.DarkSeaGreen3_1; return true; }
        | "darkseagreen2" @{ color = Color.DarkSeaGreen2; return true; }
        | "lightcyan3" @{ color = Color.LightCyan3; return true; }
        | "lightskyblue1" @{ color = Color.LightSkyBlue1; return true; }
        | "greenyellow" @{ color = Color.GreenYellow; return true; }
        | "darkolivegreen2" @{ color = Color.DarkOliveGreen2; return true; }
        | "palegreen1_1" @{ color = Color.PaleGreen1_1; return true; }
        | "darkseagreen2_1" @{ color = Color.DarkSeaGreen2_1; return true; }
        | "darkseagreen1" @{ color = Color.DarkSeaGreen1; return true; }
        | "paleturquoise1" @{ color = Color.PaleTurquoise1; return true; }
        | "red3_1" @{ color = Color.Red3_1; return true; }
        | "deeppink3" @{ color = Color.DeepPink3; return true; }
        | "deeppink3_1" @{ color = Color.DeepPink3_1; return true; }
        | "magenta3_1" @{ color = Color.Magenta3_1; return true; }
        | "magenta3_2" @{ color = Color.Magenta3_2; return true; }
        | "magenta2" @{ color = Color.Magenta2; return true; }
        | "darkorange3_1" @{ color = Color.DarkOrange3_1; return true; }
        | "indianred_1" @{ color = Color.IndianRed_1; return true; }
        | "hotpink3_1" @{ color = Color.HotPink3_1; return true; }
        | "hotpink2" @{ color = Color.HotPink2; return true; }
        | "orchid" @{ color = Color.Orchid; return true; }
        | "mediumorchid1" @{ color = Color.MediumOrchid1; return true; }
        | "orange3" @{ color = Color.Orange3; return true; }
        | "lightsalmon3_1" @{ color = Color.LightSalmon3_1; return true; }
        | "lightpink3" @{ color = Color.LightPink3; return true; }
        | "pink3" @{ color = Color.Pink3; return true; }
        | "plum3" @{ color = Color.Plum3; return true; }
        | "violet" @{ color = Color.Violet; return true; }
        | "gold3_1" @{ color = Color.Gold3_1; return true; }
        | "lightgoldenrod3" @{ color = Color.LightGoldenrod3; return true; }
        | "tan" @{ color = Color.Tan; return true; }
        | "mistyrose3" @{ color = Color.MistyRose3; return true; }
        | "thistle3" @{ color = Color.Thistle3; return true; }
        | "plum2" @{ color = Color.Plum2; return true; }
        | "darkyellow" @{ color = Color.DarkYellow; return true; }
        | "khaki3" @{ color = Color.Khaki3; return true; }
        | "lightgoldenrod2" @{ color = Color.LightGoldenrod2; return true; }
        | "lightyellow3" @{ color = Color.LightYellow3; return true; }
        | "gray84" @{ color = Color.Gray84; return true; }
        | "lightsteelblue1" @{ color = Color.LightSteelBlue1; return true; }
        | "yellow2" @{ color = Color.Yellow2; return true; }
        | "darkolivegreen1" @{ color = Color.DarkOliveGreen1; return true; }
        | "darkolivegreen1_1" @{ color = Color.DarkOliveGreen1_1; return true; }
        | "darkseagreen1_1" @{ color = Color.DarkSeaGreen1_1; return true; }
        | "honeydew2" @{ color = Color.Honeydew2; return true; }
        | "lightcyan1" @{ color = Color.LightCyan1; return true; }
        | "red1" @{ color = Color.Red1; return true; }
        | "deeppink2" @{ color = Color.DeepPink2; return true; }
        | "deeppink1" @{ color = Color.DeepPink1; return true; }
        | "deeppink1_1" @{ color = Color.DeepPink1_1; return true; }
        | "magenta2_1" @{ color = Color.Magenta2_1; return true; }
        | "magenta1" @{ color = Color.Magenta1; return true; }
        | "orangered1" @{ color = Color.OrangeRed1; return true; }
        | "indianred1" @{ color = Color.IndianRed1; return true; }
        | "indianred1_1" @{ color = Color.IndianRed1_1; return true; }
        | "hotpink" @{ color = Color.HotPink; return true; }
        | "hotpink_1" @{ color = Color.HotPink_1; return true; }
        | "mediumorchid1_1" @{ color = Color.MediumOrchid1_1; return true; }
        | "darkorange" @{ color = Color.DarkOrange; return true; }
        | "salmon1" @{ color = Color.Salmon1; return true; }
        | "lightcoral" @{ color = Color.LightCoral; return true; }
        | "palevioletred1" @{ color = Color.PaleVioletRed1; return true; }
        | "orchid2" @{ color = Color.Orchid2; return true; }
        | "orchid1" @{ color = Color.Orchid1; return true; }
        | "orange1" @{ color = Color.Orange1; return true; }
        | "sandybrown" @{ color = Color.SandyBrown; return true; }
        | "lightsalmon1" @{ color = Color.LightSalmon1; return true; }
        | "lightpink1" @{ color = Color.LightPink1; return true; }
        | "pink1" @{ color = Color.Pink1; return true; }
        | "plum1" @{ color = Color.Plum1; return true; }
        | "gold1" @{ color = Color.Gold1; return true; }
        | "lightgoldenrod2_1" @{ color = Color.LightGoldenrod2_1; return true; }
        | "lightgoldenrod2_2" @{ color = Color.LightGoldenrod2_2; return true; }
        | "navajowhite1" @{ color = Color.NavajoWhite1; return true; }
        | "mistyrose1" @{ color = Color.MistyRose1; return true; }
        | "thistle1" @{ color = Color.Thistle1; return true; }
        | "yellow1" @{ color = Color.Yellow1; return true; }
        | "lightgoldenrod1" @{ color = Color.LightGoldenrod1; return true; }
        | "khaki1" @{ color = Color.Khaki1; return true; }
        | "wheat1" @{ color = Color.Wheat1; return true; }
        | "cornsilk1" @{ color = Color.Cornsilk1; return true; }
        | "gray100" @{ color = Color.Gray100; return true; }
        | "gray3" @{ color = Color.Gray3; return true; }
        | "gray7" @{ color = Color.Gray7; return true; }
        | "gray11" @{ color = Color.Gray11; return true; }
        | "gray15" @{ color = Color.Gray15; return true; }
        | "gray19" @{ color = Color.Gray19; return true; }
        | "gray23" @{ color = Color.Gray23; return true; }
        | "gray27" @{ color = Color.Gray27; return true; }
        | "gray30" @{ color = Color.Gray30; return true; }
        | "gray35" @{ color = Color.Gray35; return true; }
        | "darkgray" @{ color = Color.DarkGray; return true; }
        | "gray42" @{ color = Color.Gray42; return true; }
        | "gray46" @{ color = Color.Gray46; return true; }
        | "gray50" @{ color = Color.Gray50; return true; }
        | "gray54" @{ color = Color.Gray54; return true; }
        | "gray58" @{ color = Color.Gray58; return true; }
        | "gray62" @{ color = Color.Gray62; return true; }
        | "gray66" @{ color = Color.Gray66; return true; }
        | "gray70" @{ color = Color.Gray70; return true; }
        | "gray74" @{ color = Color.Gray74; return true; }
        | "gray78" @{ color = Color.Gray78; return true; }
        | "gray82" @{ color = Color.Gray82; return true; }
        | "gray85" @{ color = Color.Gray85; return true; }
    );
}%%

#pragma warning disable CS0162 // Unreachable code detected
// ReSharper disable ArrangeRedundantParentheses
// ReSharper disable RedundantEmptySwitchSection
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable HeuristicUnreachableCode
// ReSharper disable RedundantJumpStatement
// ReSharper disable BadChildStatementIndent
// ReSharper disable UnreachableSwitchCaseDueToIntegerAnalysis
// ReSharper disable RedundantAssignment

using System;

namespace Std.BuildTools.Clang.Ansi;

internal static partial class AnsiColorFormatter
{
    %%write data;

    private static bool ParseColor(ReadOnlySpan<char> inputText, out Color color, out int matchedLength)
    {
        if (DoParse(inputText, out color, out matchedLength))
        {
            matchedLength += 1;
            return true;
        }

        matchedLength = -1;
        return false;

        static bool DoParse(ReadOnlySpan<char> inputText, out Color color, out int inputIndex)
        {
            var inputLength = inputText.Length;
            var currentState = tp_start;
            inputIndex = 0;
            var atEnd = inputLength;

            %%write exec;

            color = Color.Default;
            return false;
        }
    }
}
