using System;
using System.Globalization; 
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;

public class IndicesMaximusRewriteScript : MonoBehaviour
{
	//Audio and bomb info from the ModKit:
	public KMAudio mAudio;
	public KMBombModule modSelf;
	//Module components:
	public KMSelectable leftBtn,rightBtn,numeratorBtn,denominatorBtn;
	public TextMesh[] checkLabels;
	public TextMesh equationDisplay, numerTxt, denomTxt;
	public MeshRenderer[] lightRends;
	public Material onMat;

	//Constants to set bounds for the module's equations:
	private const int MinRootValue = -9, MaxRootValue = 9;
	private int[][] selectedCorrectRationalRoots;
	//Counters/trackers to track progress through the module:
	private bool resetting = true, firstPressSinceReset = false, numeratorPressed = false;
	private bool moduleSolved;
	List<int> correctRootsPressed;
	int stagesCompleted;
	int numerVal, denomVal = 1, deltaBtnHeld = 0;

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
	float timeHeld = 0f;

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
				HandleDelta(-1);
				deltaBtnHeld = -1;
			}
			return false;
		};
		leftBtn.OnInteractEnded += delegate {
			deltaBtnHeld = 0;
		};
		rightBtn.OnInteractEnded += delegate {
			deltaBtnHeld = 0;
		};
		rightBtn.OnInteract += delegate {
			if (!moduleSolved && !resetting)
			{
				mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, rightBtn.transform);
				rightBtn.AddInteractionPunch();
				HandleDelta(1);
				deltaBtnHeld = 1;
			}
			return false;
		};
		numeratorBtn.OnInteract += delegate {
			if (!moduleSolved && !resetting)
			{
				mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, numeratorBtn.transform);
				numeratorBtn.AddInteractionPunch();
				ProcessInput(true);
			}
			return false;
		};
		denominatorBtn.OnInteract += delegate {
			if (!moduleSolved && !resetting)
			{
				mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, denominatorBtn.transform);
				denominatorBtn.AddInteractionPunch();
				ProcessInput();
			}
			return false;
		};
	}

	void QuickLog(string value)
	{
		Debug.LogFormat("[{0} #{1}] {2}", modSelf.ModuleDisplayName, moduleId, value);
	}
	void QuickLog(string value, params object[] args)
	{
		QuickLog(string.Format(value, args));
	}
	void HandleDelta(int delta)
    {
		if (!firstPressSinceReset) return;
		if (numeratorPressed)
		{
			numerVal = Mathf.Clamp(numerVal + delta, MinRootValue, MaxRootValue);
			numerTxt.text = numerVal.ToString();
		}
		else
        {
			denomVal = Mathf.Clamp(denomVal + delta, 1, MaxRootValue);
			denomTxt.text = denomVal.ToString();
		}
	}
	void ProcessInput(bool isNumeratorBtn = false)
	{
		var curchkLabel = checkLabels[correctRootsPressed.Count];
		var intepretedRational = new[] { denomVal, numerVal };
		if (firstPressSinceReset && isNumeratorBtn == numeratorPressed)
		{
			if (GetGCM(denomVal, Math.Abs(numerVal)) != 1 || (numerVal == 0 && denomVal > 1))
			{// Try to prevent submitting unsimplified values like 6/3 or 0/9
				mAudio.PlaySoundAtTransform("error", transform);
				return;
            }
			var idxFoundRoot = selectedCorrectRationalRoots.IndexOf(a => intepretedRational.SequenceEqual(a));
			if (idxFoundRoot != -1)
			{
				if (!correctRootsPressed.Contains(idxFoundRoot))
				{
					QuickLog("The root, {0}, was correctly selected.", QuickConvertRational(intepretedRational));
					if (intepretedRational[0] == 1)
					{
						curchkLabel.text = intepretedRational[1].ToString();
						curchkLabel.characterSize = .25f;
					}
					else
					{
						curchkLabel.text = string.Format("{0}     \n/\n    {1}", intepretedRational[1], intepretedRational[0]);
						curchkLabel.characterSize = 0.15f;
					}
					curchkLabel.color = Color.green;
					correctRootsPressed.Add(idxFoundRoot);
				}
				if (correctRootsPressed.Count() >= 4)
				{
					if (stagesCompleted > 0)
					{
						moduleSolved = true;
						QuickLog("You got all of the correct roots. Module disarmed.");
						StartCoroutine(SuccessTextRoutine());
					}
					else
                    {
						QuickLog("You got all of the correct roots. One more time.");
						StartCoroutine(DelayNextStage());
					}
				}
			}
			else
			{
				QuickLog("The root, {0}, was incorrectly selected. Starting over...", QuickConvertRational(intepretedRational));
				modSelf.HandleStrike();
				StartCoroutine(StrikeRoutine(curchkLabel));
			}
		}
		if (!isNumeratorBtn && stagesCompleted == 0)
        {
			mAudio.PlaySoundAtTransform("error", transform);
			return;
		}
		firstPressSinceReset = true;
		numeratorPressed = isNumeratorBtn;
		numerTxt.color = numeratorPressed ? Color.yellow : Color.white;
		denomTxt.color = numeratorPressed ? Color.white : Color.yellow;
	}
	string QuickConvertRational(int[] expression)
    {
		return expression[0] == 1 ? (expression[1]).ToString() : string.Format("{0}/{1}", expression[1], expression[0]);
	}
	int GetGCM(int a, int b)
    {
		var maxVal = Mathf.Max(a, b);
		var minVal = Mathf.Min(a, b);
		while (minVal > 0)
        {
			var nextMin = maxVal % minVal;
			maxVal = minVal;
			minVal = nextMin;
        }
		return maxVal;
    }
	void GenerateSolution(bool integerRoots = true)
	{
		correctRootsPressed = new List<int>();
		if (integerRoots)
		{
			var allPossibleRoots = Enumerable.Range(MinRootValue, MaxRootValue - MinRootValue + 1).ToArray().Shuffle(); // Shuffle the array.
			selectedCorrectRationalRoots = allPossibleRoots.Take(4).Select(a => new[] { 1, a }).ToArray();
		}
		else
        {
			selectedCorrectRationalRoots = new int[4][];
			retryRationalGuarentee:
            for (var x = 0; x < 4; x++)
            {
				retryRational:
				var a = Random.Range(1, MaxRootValue + 1);
				var b = Random.Range(1, MaxRootValue + 1);
				while (GetGCM(a, b) != 1)
                {
					a = Random.Range(1, MaxRootValue + 1);
					b = Random.Range(1, MaxRootValue + 1);
				}
				if (Random.value < 0.5f)
					b *= -1;
				var nextRoot = new[] { a, b };
				if (selectedCorrectRationalRoots.Take(x).Any(n => n.SequenceEqual(nextRoot)))
					goto retryRational;
				selectedCorrectRationalRoots[x] = nextRoot;
			}
			if (selectedCorrectRationalRoots.All(a => a[0] == 1))
				goto retryRationalGuarentee;
        }
		QuickLog("All distinct roots selected (lowest to highest): {0}",
			selectedCorrectRationalRoots.OrderBy(a => a[1]).ThenBy(a => a[0]).Select(a => QuickConvertRational(a)).Join());
		var repeatCount = Enumerable.Repeat(1, 4).ToArray();
		for (var x = 0; x < 4; x++)
		{
			var selectedIdx = Enumerable.Range(0, 4).PickRandom();
			repeatCount[selectedIdx]++;
		}
		QuickLog("Each of the following roots will occur this many times in the following equation: {0}", Enumerable.Range(0, 4).OrderBy(a => selectedCorrectRationalRoots.ElementAt(a)[1]).ThenBy(a => selectedCorrectRationalRoots.ElementAt(a)[0])
			.Select(a => string.Format("[{0}: {1}]", QuickConvertRational(selectedCorrectRationalRoots[a]), repeatCount[a])).Join(", "));

		int[] arrayToDisplay = new int[0];
		for (var x = 0; x < selectedCorrectRationalRoots.Length; x++)
		{
			var flippedRoot = selectedCorrectRationalRoots[x].ToArray();
			flippedRoot[1] *= -1;
			for (var y = 0; y < repeatCount[x]; y++)
				arrayToDisplay = PolynomialMultiplication(arrayToDisplay, flippedRoot);
		}
		var equationMade = CreateEquationString(arrayToDisplay);
		QuickLog("Generated Equation: {0}", equationMade);
		SetEquationText(equationMade);
		numerTxt.text = numerVal.ToString();
		denomTxt.text = denomVal.ToString();
		denomTxt.color = Color.white;
		numerTxt.color = Color.white;
		for (var x = 0; x < checkLabels.Length; x++)
			checkLabels[x].text = "";
		resetting = false;
		firstPressSinceReset = false;
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
					equationString += (i == 0 ? "" : "+") + coefficients[i];
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

		return equationString;
	}

	//Formats the equation text appropriately so it stays on the screen, then renders it.
	void SetEquationText(string equation)
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
		equationDisplay.characterSize = stagesCompleted == 0 ? 0.125f : 0.1f;
	}
	//Pauses the module on a strike while the offending button is shown in red font.
	IEnumerator StrikeRoutine(TextMesh offendingButton)
	{
		resetting = true;
		offendingButton.color = Color.red;
		var intepretedRational = new[] { denomVal, numerVal };
		if (intepretedRational[0] == 1)
		{
			offendingButton.text = intepretedRational[1].ToString();
			offendingButton.characterSize = .25f;
		}
		else
		{
			offendingButton.text = string.Format("{0}     \n/\n    {1}", intepretedRational[1], intepretedRational[0]);
			offendingButton.characterSize = 0.15f;
		}
		yield return new WaitForSeconds(1f);
		GenerateSolution(stagesCompleted == 0);
		resetting = false;
	}

	IEnumerator DelayNextStage()
    {
		resetting = true;
		yield return new WaitForSeconds(0.25f);
		mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
		yield return new WaitForSeconds(0.25f);
		lightRends[stagesCompleted].material = onMat;
		stagesCompleted++;
		GenerateSolution(false);
		resetting = false;
	}

	//Makes a "module disarmed message" appear on the display.
	IEnumerator SuccessTextRoutine()
	{
		string successText = "System Status:\nDisarmed.";
		int index = 0;
		yield return new WaitForSeconds(1f);
		stagesCompleted++;
		lightRends[1].material = onMat;
		mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
		while (equationDisplay.text.Count(a => a != '\n') > 0)
		{
			yield return null;
			string[] splittedText = equationDisplay.text.Split('\n');
			for (var x = 0; x < splittedText.Length; x++)
			{
				if (splittedText[x].Any())
					splittedText[x] = splittedText[x].Substring(0, splittedText[x].Length - 1);
			}
			equationDisplay.text = splittedText.Join("\n");
			mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.TypewriterKey, transform);
		}
		equationDisplay.characterSize = 0.125f;
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
	void Update()
    {
		if (deltaBtnHeld == 0)
			timeHeld = 0;
		else
        {
			timeHeld += Time.deltaTime;
			if (timeHeld > 0.5f)
            {
				timeHeld = 0.4f;
				HandleDelta(deltaBtnHeld);
            }
        }
    }


	IEnumerator TwitchHandleForcedSolve()
	{
		while (stagesCompleted < 2)
		{
			while (resetting)
				yield return true;
			var remainingRoots = Enumerable.Range(0, 4).Except(correctRootsPressed).OrderBy(a => selectedCorrectRationalRoots[a][1]);
			foreach (var rootIdx in remainingRoots)
			{
				var curRoot = selectedCorrectRationalRoots[rootIdx];
				while (denomVal != curRoot[0])
				{
					if (numeratorPressed || !firstPressSinceReset)
						denominatorBtn.OnInteract();
					if (denomVal < curRoot[0])
					{
						rightBtn.OnInteract();
						rightBtn.OnInteractEnded();
					}
					else
					{
						leftBtn.OnInteract();
						leftBtn.OnInteractEnded();
					}
					yield return new WaitForSeconds(.1f);
				}
				while (numerVal != curRoot[1])
				{
					if (!numeratorPressed || !firstPressSinceReset)
						numeratorBtn.OnInteract();
					if (numerVal < curRoot[1])
					{
                        rightBtn.OnInteract();
                        rightBtn.OnInteractEnded();
                    }
                    else
                    {
                        leftBtn.OnInteract();
                        leftBtn.OnInteractEnded();
                    }
					yield return new WaitForSeconds(.1f);
				}
				if (!firstPressSinceReset && denomVal == curRoot[0] && numerVal == curRoot[1])
				{
					numeratorBtn.OnInteract();
					yield return new WaitForSeconds(.1f);
				}
				(numeratorPressed ? numeratorBtn : denominatorBtn).OnInteract();
				yield return new WaitForSeconds(.1f);
			}
			yield return true;
		}
		while (!moduleSolved)
			yield return true;
	}
	
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "Submit roots for x with “!{0} submit # #/#”. Possible roots are -9 to 9 inclusive. Multiple roots can be submitted in the same command by space out the numbers.";
#pragma warning restore 414

	//Process command for Twitch Plays - IEnumerator method used due to the win sequence taking roughly 1 second.
	IEnumerator ProcessTwitchCommand(string command)
	{
		var intCmd = command.Trim();
		var rgxCmdValue = Regex.Match(intCmd, @"^submit(\s\-?[0-9](/[1-9])?)+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (rgxCmdValue.Success)
        {
			var obtainedStr = rgxCmdValue.Value.Split().Skip(1);
			var valuesToSubmit = new List<int[]>();
			foreach (var str in obtainedStr)
			{
				if (str.RegexMatch(@"\-?[0-9]/[1-9]"))
				{
					if (stagesCompleted == 0)
                    {
						yield return "sendtochaterror The module does not allow fractions to be included right now.";
						yield break;
                    }
					valuesToSubmit.Add(str.Split('/').Select(a => int.Parse(a)).ToArray());
				}
				else
					valuesToSubmit.Add(new[] { int.Parse(str), 1 });
			}
			yield return null;
			foreach (var possibleRoot in valuesToSubmit)
			{
				//Debug.Log(possibleRoot.Reverse().Join("/"));
				while (numerVal != possibleRoot[0])
				{
					if (!numeratorPressed || !firstPressSinceReset)
						numeratorBtn.OnInteract();
					if (numerVal < possibleRoot[0])
					{
						rightBtn.OnInteract();
						rightBtn.OnInteractEnded();
					}
					else
					{
						leftBtn.OnInteract();
						leftBtn.OnInteractEnded();
					}
					yield return new WaitForSeconds(.1f);
				}
				while (denomVal != possibleRoot[1])
				{
					if (numeratorPressed || !firstPressSinceReset)
						denominatorBtn.OnInteract();
					if (denomVal < possibleRoot[1])
					{
						rightBtn.OnInteract();
						rightBtn.OnInteractEnded();
					}
					else
					{
						leftBtn.OnInteract();
						leftBtn.OnInteractEnded();
					}
					yield return new WaitForSeconds(.1f);
				}
				if (!firstPressSinceReset && denomVal == possibleRoot[1] && numerVal == possibleRoot[0])
                {
					numeratorBtn.OnInteract();
					yield return new WaitForSeconds(.1f);
				}
				(numeratorPressed ? numeratorBtn : denominatorBtn).OnInteract();
				yield return new WaitForSeconds(.1f);
				if (moduleSolved)
                {
					yield return "solve";
					yield break;
                }
				if (resetting)
					yield break;
			}
		}
		yield break;
	}
}