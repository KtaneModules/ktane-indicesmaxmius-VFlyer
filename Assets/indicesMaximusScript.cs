using System;
using System.Globalization; 
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using rnd = UnityEngine.Random;

public class indicesMaximusScript : MonoBehaviour
{
	//Audio and bomb info from the ModKit:
	public KMAudio mAudio;
	public KMBombModule modSelf;
	//Module components:
	public KMSelectable[] numButtons;
	public TextMesh[] numLabels;
	public TextMesh equationDisplay;

	//Constants to set bounds for the module's equations:
	private const int MinRootValue = -9, MaxRootValue = 9;
	private int[] selectedPossibleRoots;
	private bool[] isCorrectRoot, pressedRoot;
	//Counters/trackers to track progress through the module:
	private bool resetting = true;
	private bool moduleSolved;

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

		for (int i = 0; i < numButtons.Length; i++)
		{
			KMSelectable number = numButtons[i];
			var y = i;
			number.OnInteract += delegate { ProcessInput(y); return false; };
		}
	}

	void QuickLog(string value)
	{
		Debug.LogFormat("[Indices Maximus #{0}] {1}", moduleId, value);
	}
	void ProcessInput(int idx)
	{
		if (idx < 0 || idx >= numButtons.Length || moduleSolved || resetting) return;
		mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, numButtons[idx].transform);
		numButtons[idx].AddInteractionPunch();
		if (isCorrectRoot[idx])
		{
			if (!pressedRoot[idx])
				QuickLog(string.Format("The root, {0}, was correctly pressed.", selectedPossibleRoots[idx]));
			pressedRoot[idx] = true;
			numLabels[idx].color = Color.green;
			if (pressedRoot.SequenceEqual(isCorrectRoot))
			{
				moduleSolved = true;
				QuickLog("You got all of the correct roots. Module disarmed.");
				StartCoroutine(SuccessTextRoutine());
			}
		}
		else
		{
			QuickLog(string.Format("The root, {0}, was incorrectly pressed. Starting over...", selectedPossibleRoots[idx]));
			modSelf.HandleStrike();
			StartCoroutine(StrikeRoutine(numLabels[idx]));

		}
	}
	void GenerateSolution()
	{
		var allPossibleRoots = Enumerable.Range(MinRootValue, MaxRootValue - MinRootValue + 1).ToArray().Shuffle(); // Shuffle the array.;
		selectedPossibleRoots = allPossibleRoots.Take(8).ToArray(); // Then take the first 8 values that were shuffled and conjoin them into an array.
		QuickLog(string.Format("All possible roots selected: {0}", selectedPossibleRoots.Join()));
		var idxSolutions = Enumerable.Range(0, 8).ToArray().Shuffle().Take(4); // Now take the first 4 indexes of all of the selected roots.
		var allCorrectRoots = idxSolutions.Select(a => selectedPossibleRoots[a]);
		isCorrectRoot = new bool[8];
		pressedRoot = new bool[8];
		for (var x = 0; x < idxSolutions.Count(); x++)
		{
			isCorrectRoot[idxSolutions.ElementAt(x)] = true;
		}
		QuickLog(string.Format("All solution roots selected (lowest to highest): {0}", idxSolutions.Select(a => selectedPossibleRoots[a]).OrderBy(a => a).Join()));
		var repeatCount = Enumerable.Repeat(1, 4).ToArray();
		for (var x = 0; x < 4; x++)
		{
			var selectedIdx = Enumerable.Range(0, 4).PickRandom();
			repeatCount[selectedIdx]++;
		}
		QuickLog(string.Format("Each of the following roots will occur this many times in the following equation: {0}", Enumerable.Range(0, 4).OrderBy(a => allCorrectRoots.ElementAt(a)).Select(a => "[" + selectedPossibleRoots[idxSolutions.ElementAt(a)].ToString() + ": " + repeatCount[a] + "]").Join(", ")));

		int[] arrayToDisplay = new int[0];
		for (var x = 0; x < allCorrectRoots.Count(); x++)
		{
			for (var y = 0; y < repeatCount[x]; y++)
				arrayToDisplay = PolynomialMultiplication(arrayToDisplay, new[] { 1, -allCorrectRoots.ElementAt(x) });
		}
		var equationMade = CreateEquationString(arrayToDisplay);
		QuickLog(string.Format("Generated Equation: {0}", equationMade));
		SetEquationtext(equationMade);
		for (var x = 0; x < numLabels.Length; x++)
		{
			numLabels[x].text = selectedPossibleRoots[x].ToString();
			numLabels[x].color = Color.white;
		}
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

		return "0=" + equationString;
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
		while (resetting)
			yield return true;
		for (var x = 0; x < isCorrectRoot.Length; x++)
        {
			if (isCorrectRoot[x] && !pressedRoot[x])
			{
				numButtons[x].OnInteract();
				yield return new WaitForSeconds(0.1f);
			}
        }
	}
	
#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Submit roots for x with “!{0} press 1 -1”. Multiple roots can be submitted in the same command by space out the numbers.";
#pragma warning restore 414

	//Process command for Twitch Plays - IEnumerator method used due to the win sequence taking roughly 1 second.
	IEnumerator ProcessTwitchCommand(string command)
	{
		var match = Regex.Match(command,@"^\s*press(\s((-?[1-9])|0))+$", RegexOptions.IgnoreCase);
		
		if(!match.Success)
			yield break;
			
		var pressed  = match.Groups[0].Value.ToLowerInvariant().Trim();
		string[] parameters = pressed.ToString().Split();
		List<KMSelectable> allButtonsToPress = new List<KMSelectable>();
        for (var x = 1; x < parameters.Length; x++)
        {
			int aValue;
			if (!int.TryParse(parameters[x], out aValue))
				{
					yield return string.Format("sendtochaterror The given value \"{0}\" is not valid.", parameters[x]);
					yield break;
				}
			var idx = selectedPossibleRoots.IndexOf(a => a == aValue);
			if (idx == -1)
            {
				yield return string.Format("sendtochaterror The given value \"{0}\" does not exist as any possible buttons.",parameters[x]);
				yield break;
            }
			allButtonsToPress.Add(numButtons[idx]);
        }
		
		foreach (KMSelectable aButton in allButtonsToPress)
		{
			yield return null;
			aButton.OnInteract();
			if (moduleSolved)
				yield return "solve";
			if (resetting)
				yield break;
		}
	}
}