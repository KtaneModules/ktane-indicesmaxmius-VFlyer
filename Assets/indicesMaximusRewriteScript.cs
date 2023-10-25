using System;
using System.Globalization; 
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using rnd = UnityEngine.Random;

public class indicesMaximusRewriteScript : MonoBehaviour
{
	//Audio and bomb info from the ModKit:
	public KMAudio mAudio;
	public KMBombModule modSelf;
	//Module components:
	public KMSelectable leftBtn,rightBtn,centerBtn;
	public TextMesh[] checkLabels;
	public TextMesh equationDisplay, labelTxt;

	//Constants to set bounds for the module's equations:
	private const int MinRootValue = -9, MaxRootValue = 9;
	private int[] selectedCorrectRoots;
	//Counters/trackers to track progress through the module:
	private bool resetting = true;
	private bool moduleSolved;
	List<int> correctRootsPressed;
	int curValue;

	//Useful unicode constants for writing the equations:
	private const string SuperTwo = "\u00B2",
		SuperThree = "\u00B3",
		SuperFour = "\u2074",
		SuperFive = "\u2075",
		SuperSix = "\u2076",
		SuperSeven = "\u2077",
		SuperEight = "\u2078";

	//Logging variables:
	static int moduleIdCounter = 1;
	int moduleId;

	//Initialize module.
	void Start()
	{
		moduleId = moduleIdCounter++;
		GenerateSolution();
		leftBtn.OnInteract += delegate {
			if (!moduleSolved && !resetting)
			{
				mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, leftBtn.transform);
				leftBtn.AddInteractionPunch();
				curValue = Mathf.Clamp(curValue - 1, MinRootValue, MaxRootValue);
				labelTxt.text = curValue.ToString();
			}
			return false;
		};
		rightBtn.OnInteract += delegate {
			if (!moduleSolved && !resetting)
			{
				mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, rightBtn.transform);
				rightBtn.AddInteractionPunch();
				curValue = Mathf.Clamp(curValue + 1, MinRootValue, MaxRootValue);
				labelTxt.text = curValue.ToString();
			}
			return false;
		};
		centerBtn.OnInteract += delegate {
			if (!moduleSolved && !resetting)
			{
				mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, rightBtn.transform);
				rightBtn.AddInteractionPunch();
				ProcessInput();
			}
			return false;
		};
	}

	void QuickLog(string value)
	{
		Debug.LogFormat("[Indices Maximus #{0}] {1}", moduleId, value);
	}
	void ProcessInput()
	{
		var curchkLabel = checkLabels[correctRootsPressed.Count];
		if (selectedCorrectRoots.Contains(curValue))
		{
			if (!correctRootsPressed.Contains(curValue))
			{
				QuickLog(string.Format("The root, {0}, was correctly selected.", curValue));
				curchkLabel.text = curValue.ToString();
				curchkLabel.color = Color.green;
				correctRootsPressed.Add(curValue);
			}
			if (selectedCorrectRoots.Intersect(correctRootsPressed).Count() == 4)
			{
				moduleSolved = true;
				QuickLog("You got all of the correct roots. Module disarmed.");
				StartCoroutine(SuccessTextRoutine());
			}
		}
		else
		{
			QuickLog(string.Format("The root, {0}, was incorrectly selected. Starting over...", curValue));
			modSelf.HandleStrike();
			StartCoroutine(StrikeRoutine(curchkLabel));

		}
	}
	void GenerateSolution()
	{
		correctRootsPressed = new List<int>();

		var allPossibleRoots = Enumerable.Range(MinRootValue, MaxRootValue - MinRootValue + 1).ToArray().Shuffle(); // Shuffle the array.;
		selectedCorrectRoots = allPossibleRoots.Take(4).ToArray();

		QuickLog(string.Format("All distinct roots selected (lowest to highest): {0}", selectedCorrectRoots.OrderBy(a => a).Join()));
		var repeatCount = Enumerable.Repeat(1, 4).ToArray();
		for (var x = 0; x < 4; x++)
		{
			var selectedIdx = Enumerable.Range(0, 4).PickRandom();
			repeatCount[selectedIdx]++;
		}
		QuickLog(string.Format("Each of the following roots will occur this many times in the following equation: {0}", Enumerable.Range(0, 4).OrderBy(a => selectedCorrectRoots.ElementAt(a))
			.Select(a => string.Format("[{0}: {1}]", selectedCorrectRoots[a].ToString(), repeatCount[a])).Join(", ")));

		int[] arrayToDisplay = new int[0];
		for (var x = 0; x < selectedCorrectRoots.Length; x++)
		{
			for (var y = 0; y < repeatCount[x]; y++)
				arrayToDisplay = PolynomialMultiplication(arrayToDisplay, new[] { 1, -selectedCorrectRoots.ElementAt(x) });
		}
		var equationMade = CreateEquationString(arrayToDisplay);
		QuickLog(string.Format("Generated Equation: {0}", equationMade));
		SetEquationtext(equationMade);
		labelTxt.text = curValue.ToString();
		for (var x = 0; x < checkLabels.Length; x++)
			checkLabels[x].text = "";
		resetting = false;
	}
	int[] PolynomialMultiplication(int[] oneArray, int[] anotherArray)
	{
		/* Summary: Multiply 2 polynomials as if it was in 2 arrays.
		 * Example:
		 * (x-1)(x+5)
		 * (x)x+5*x
		 *     -1*x+5*-1
		 * x^2+5x
		 *    -1x-5
		 * x^2+4x-5
		 */
		if (oneArray.Length == 0) return anotherArray; // If the current array length is 0, return the other array instead.
		else if (anotherArray.Length == 0) return oneArray; // If the other array length is 0, return the current array instead.
		int[] output = new int[oneArray.Length + anotherArray.Length - 1];
		for (var x = 0; x < anotherArray.Length; x++)
		{
			for (var y = 0; y < oneArray.Length; y++)
			{
				output[x + y] += oneArray[y] * anotherArray[x];
			}
		}
		return output;
	}

	//Form a string to display the equation on the module (and in logging). 
	string CreateEquationString(int[] coefficients)
	{
		string equationString = "";

		for (int i = 0; i < coefficients.Length; i++)
		{
			if (coefficients[i] != 0)
			{
				if (coefficients[i] < -1 || (i == (coefficients.Length - 1) && coefficients[i] == -1))
					equationString += coefficients[i];
				else if (coefficients[i] > 1 || (i == (coefficients.Length - 1) && coefficients[i] == 1))
					equationString += "+" + coefficients[i];
				else if (i != 0 && coefficients[i] == 1)
					equationString += "+";
				else if (i != 0)
					equationString += "-";

				switch ((coefficients.Length - i) - 1)
				{
					case 1: equationString += "x"; break;
					case 2: equationString += "x" + SuperTwo; break;
					case 3: equationString += "x" + SuperThree; break;
					case 4: equationString += "x" + SuperFour; break;
					case 5: equationString += "x" + SuperFive; break;
					case 6: equationString += "x" + SuperSix; break;
					case 7: equationString += "x" + SuperSeven; break;
					case 8: equationString += "x" + SuperEight; break;
					default: break;
				}
			}
		}

		return "" + equationString;
	}

	//Formats the equation text appropriately so it stays on the screen, then renders it.
	void SetEquationtext(string equation)
	{
		string equationToShow = "";
		int termsInLine = 0;

		for (int i = 0; i < equation.Length; i++)
		{
			if (equation[i] == '+' || equation[i] == '-')
				termsInLine++;

			if (termsInLine > 2)
			{
				termsInLine = 0;
				equationToShow += "\n";
			}

			equationToShow += equation[i];
		}
		equationDisplay.text = equationToShow;
	}
	//Pauses the module on a strike while the offending button is shown in red font.
	IEnumerator StrikeRoutine(TextMesh offendingButton)
	{
		resetting = true;
		offendingButton.color = new Color32(255, 0, 0, 255);
		offendingButton.text = curValue.ToString();
		yield return new WaitForSeconds(1f);
		GenerateSolution();
	}

	//Makes a "module disarmed message" appear on the display.
	IEnumerator SuccessTextRoutine()
	{
		string successText = "System Status:\nDisarmed.";
		int index = 0;
		yield return new WaitForSeconds(1f);
		mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
		while (equationDisplay.text.Count(a => a != '\n') > 0)
		{
			yield return null;
			string[] splittedText = equationDisplay.text.Split('\n');
			for (var x = 0; x < splittedText.Length; x++)
			{
				if (splittedText[x].Length > 0)
					splittedText[x] = splittedText[x].Substring(0, splittedText[x].Length - 1);
			}
			equationDisplay.text = splittedText.Join("\n");
			mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.TypewriterKey, transform);
		}

		while (index < successText.Length)
		{
			yield return new WaitForSeconds(.025f);
			index++;
			equationDisplay.text = successText.Substring(0, index);
			mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.TypewriterKey, transform);
		}
		mAudio.PlaySoundAtTransform("success", transform);
		modSelf.HandlePass();
	}


	IEnumerator TwitchHandleForcedSolve()
	{
		var remainingRoots = selectedCorrectRoots.Except(correctRootsPressed).OrderBy(a => a);
		foreach (var root in remainingRoots)
        {
			while (curValue != root)
            {
				if (curValue < root)
					rightBtn.OnInteract();
				else
					leftBtn.OnInteract();
				yield return new WaitForSeconds(.1f);
            }
			centerBtn.OnInteract();
			yield return new WaitForSeconds(.1f);
		}
		while (moduleSolved)
			yield return true;
	}
	
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "Submit roots for x with “!{0} submit #”. Roots are numbers -9 to 9 inclusive. Multiple roots can be submitted in the same command by space out the numbers.";
#pragma warning restore 414

	//Process command for Twitch Plays - IEnumerator method used due to the win sequence taking roughly 1 second.
	IEnumerator ProcessTwitchCommand(string command)
	{
		var intCmd = command.Trim();
		var rgxCmdValue = Regex.Match(intCmd, @"^submit(\s\-?[0-9])+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (rgxCmdValue.Success)
        {
			var obtainedStr = rgxCmdValue.Value.Split().Skip(1);
			var valuesToSubmit = new List<int>();
			foreach (var str in obtainedStr)
				valuesToSubmit.Add(int.Parse(str));
			yield return null;
            for (var x = 0; x < valuesToSubmit.Count; x++)
            {
				var curTargetVal = valuesToSubmit[x];
				while (curValue != curTargetVal)
                {
					if (curValue < curTargetVal)
						rightBtn.OnInteract();
					else
						leftBtn.OnInteract();
					yield return new WaitForSeconds(.1f);
                }
				centerBtn.OnInteract();
				yield return new WaitForSeconds(.1f);
				if (moduleSolved)
                {
					yield return "solve";
					yield break;
                }
				else if (resetting)
					yield break;
			}
        }
		yield break;
	}
}