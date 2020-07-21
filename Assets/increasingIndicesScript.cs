using System;
using System.Globalization; 
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;

public class increasingIndicesScript : MonoBehaviour 
{
	//Audio and bomb info from the ModKit:
	public KMAudio Audio;

	//Module components:
	public KMSelectable[] numButtons;
	public Renderer equationDisplay;
	public Renderer[] stageLights;
	public Material[] lightColors;

	//Equation and solution storage [Pos 0 = Stage 1 etc.]:
	public int[][] solutionRoots = new int[3][];
	public int[][] equationCoefficients = new int[3][];
	public String[] equationStrings = new String[3];

	//Constants to set bounds for the module's equations:
	private const int MinRootValue = -3;
	private const int MaxRootValue = 4;

	//Counters/trackers to track progress through the module:
	private int stageNum = 0;
	private List<String> correctPresses;
	private bool resetting;
	private bool moduleSolved;

	//Useful unicode constants for writing the equations:
	private const String SuperTwo = "\u00B2";
	private const String SuperThree = "\u00B3";
	private const String SuperFour = "\u2074";
	
	//Logging variables:
	static int moduleIdCounter = 1;
	int moduleId;

	//Awaken module.
	void Awake()
	{
		moduleId = moduleIdCounter++;
		foreach(KMSelectable number in numButtons)
		{
			KMSelectable pressedNumber = number;
			number.OnInteract += delegate(){PressNumber(pressedNumber); return false;};
		}
	}

	//Initialize module.
	void Start() 
	{
		stageNum = 1;
		DisplayStage();
	}

	//Randomly generate roots for x from which an equation of the required degree can be created.
	int[] GenerateRoots(int degree)
	{
		int[] roots = new int[degree];

		for(int i = 0; i < degree; i++)
		{
			roots[i] = UnityEngine.Random.Range(MinRootValue, MaxRootValue + 1);
		}

		Array.Sort(roots);
		return roots;
	}

	//Use the roots to produce an array of coefficients in decreasing powers of x.
	int[] GenerateEquationFromRoots(int[] roots)
	{
		if(roots.Length == 2)
			return new int[3] {1, -(roots.Sum()), roots.Aggregate(1, (a, b) => a * b)};
		else if(roots.Length == 3)
			return new int[4] {1, -(roots.Sum()), MultiplyAllPairs(roots), -roots.Aggregate(1, (a, b) => a * b)};
		else
			return new int[5] {1, -(roots.Sum()), MultiplyAllPairs(roots), -MultiplyAllTriples(roots), roots.Aggregate(1, (a, b) => a * b)};
	}

	//Multiply all pairs of elements in an array, to help with generating coefficients.
	int MultiplyAllPairs(int[] roots)
	{
		int sumOfPairProducts = 0;

		for(int i = 0; i < roots.Length - 1; i++)
		{
			for(int j = i+1; j < roots.Length; j++)
				sumOfPairProducts += roots[i] * roots[j];
		}

		return sumOfPairProducts;
	}

	//Multiply all triples of elements in an array, to help with generating coefficients.
	int MultiplyAllTriples(int[] roots)
	{
		int sumOfTripleProducts = 0;

		for(int i = 0; i < roots.Length; i++)
		{
			for(int j = i+1; j < roots.Length; j++)
			{
				for(int k = j+1; k < roots.Length; k++)
					sumOfTripleProducts += (roots[i] * roots[j] * roots[k]);
			}
		}

		return sumOfTripleProducts;
	}

	//Form a String to display the equation on the module (and in logging). 
	String CreateEquationString(int[] coefficients)
	{
		String equationString = "";

		for(int i = 0; i < coefficients.Length; i++)
		{
			if(coefficients[i] != 0)
			{
				if(coefficients[i] < -1 || (i == (coefficients.Length - 1) && coefficients[i] == -1))
					equationString += coefficients[i];
				else if(coefficients[i] > 1 || (i == (coefficients.Length - 1) && coefficients[i] == 1))
					equationString += "+" + coefficients[i];
				else if(i != 0 && coefficients[i] == 1)
					equationString += "+";
				else if(i != 0)
					equationString += "-";

				switch((coefficients.Length - i) - 1)
				{
					case 1 : equationString += "x"; break;
					case 2 : equationString += "x" + SuperTwo; break;
					case 3 : equationString += "x" + SuperThree; break;
					case 4 : equationString += "x" + SuperFour; break;
					default : break;
				}
			}
		}

		return equationString;
	}

	//Process defuser pressing one of the eight number buttons
	void PressNumber(KMSelectable number)
	{
		if(moduleSolved || resetting)
			return;

		String numberLabelPressed = number.GetComponentInChildren<TextMesh>().text;

		Debug.LogFormat("[Increasing Indices #{0}] You pressed {1}", moduleId, numberLabelPressed);

		if(solutionRoots[stageNum-1].Contains(int.Parse(numberLabelPressed, NumberStyles.AllowLeadingSign)) && !correctPresses.Contains(numberLabelPressed))
		{//If the defuser has pressed a correct button they had not already pressed, we must add it to the list of correct buttons pressed.
			number.AddInteractionPunch();
			correctPresses.Add(numberLabelPressed);
			
			number.GetComponentInChildren<TextMesh>().color = new Color32(0,255,0,255);
			if(correctPresses.Count(s => s != null) == solutionRoots[stageNum-1].Distinct().Count())
			{//If the defuser has now pressed all correct buttons, we must increase the stage or pass the module.
				Debug.LogFormat("[Increasing Indices #{0}] Stage passed.", moduleId);
				StartCoroutine(StagePassedRoutine());
			}  
			GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
		}
		else if(!solutionRoots[stageNum-1].Contains(int.Parse(numberLabelPressed, NumberStyles.AllowLeadingSign)))
		{//If the defuser has pressed a button which is NOT correct, register a strike and reset the stage.
			Debug.LogFormat("[Increasing Indices #{0}] {1} is not a valid solution for x. Strike!", moduleId, numberLabelPressed);
			GetComponent<KMBombModule>().HandleStrike();
			StartCoroutine(StrikeRoutine(number));
		}
	}

	//Progresses the module to the next stage, creating new equations and setting the display accordingly.
	void DisplayStage()
	{
		if(stageNum > 3)
		{
			moduleSolved = true;
			Debug.LogFormat("[Increasing Indices #{0}] Module solved.", moduleId);
			equationDisplay.GetComponentInChildren<TextMesh>().fontSize = 108;
			StartCoroutine(SuccessTextRoutine());
			return;
		}

		foreach(KMSelectable number in numButtons)
		{
			KMSelectable numberToColor = number;
			numberToColor.GetComponentInChildren<TextMesh>().color = new Color32(255,255,255,255);			
		}

		solutionRoots[stageNum-1] = GenerateRoots(stageNum + 1);
		correctPresses = new List<String>();
		equationCoefficients[stageNum-1] = GenerateEquationFromRoots(solutionRoots[stageNum-1]);
		equationStrings[stageNum-1] = CreateEquationString(equationCoefficients[stageNum-1]);
		SetEquationtext(equationStrings[stageNum-1]);

		Debug.LogFormat("[Increasing Indices #{0}] Commencing stage {1}.", moduleId, stageNum);
		Debug.LogFormat("[Increasing Indices #{0}] The equation is {1}", moduleId, equationStrings[stageNum-1]);
		Debug.LogFormat("[Increasing Indices #{0}] The correct roots are: {1}.", moduleId, String.Join(", ", (solutionRoots[stageNum-1].Distinct()).Select(x => x.ToString()).ToArray()));
	}

	//Formats the equation text appropriately so it stays on the screen, then renders it.
	void SetEquationtext(String equation)
	{
		String equationToShow = "";
		int termsInLine = 0;

		for(int i = 0; i < equation.Length; i++)
		{
			if(equation[i] == '+' || equation[i] == '-')
				termsInLine++;

			if(termsInLine > 2)
			{
				termsInLine = 0;
				equationToShow += "\n";
			}

			equationToShow += equation[i];
		}
		equationDisplay.GetComponentInChildren<TextMesh>().text = equationToShow;
	}

	//Pauses the module before progressing to the next stage while the correctly-pressed buttons are shown in green font.
	IEnumerator StagePassedRoutine()
	{
		resetting = true;
		yield return new WaitForSeconds(0.25f);
		GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);	
		yield return new WaitForSeconds(0.25f);
		stageLights[stageNum-1].material = lightColors[1];
		stageNum++;
		DisplayStage();
		resetting = false;
	}
	
	//Pauses the module on a strike while the offending button is shown in red font.
	IEnumerator StrikeRoutine(KMSelectable offendingButton)
	{
		resetting = true;
		offendingButton.GetComponentInChildren<TextMesh>().color = new Color32(255,0,0,255);
		yield return new WaitForSeconds(1f);	
		DisplayStage();
		resetting = false;
	}

	//Makes a "module disarmed message" appear on the display.
	IEnumerator SuccessTextRoutine()
	{
		String successText = "System Status:\nDisarmed.";
		int index = 0;

		while(index < successText.Length)
		{
			yield return new WaitForSeconds(0.05f);
			index++;
			equationDisplay.GetComponentInChildren<TextMesh>().text = successText.Substring(0,index);
			GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.TypewriterKey, transform);
		}
		Audio.PlaySoundAtTransform("success", transform);
		GetComponent<KMBombModule>().HandlePass();
	}

	
#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Submit roots for x with “!{0} press 1 -1”.";
#pragma warning restore 414

	//Process command for Twitch Plays - IEnumerator method used due to the win sequence taking roughly 1 second.
	IEnumerator ProcessTwitchCommand(String command)
	{
		var match = Regex.Match(command,@"^\s*press(\s((-?[1-3])|0|4))+$", RegexOptions.IgnoreCase);
		List<KMSelectable> buttonsToPress = new List<KMSelectable>();

		if(!match.Success)
			yield break;
			
		var pressed  = match.Groups[0].Value.ToLowerInvariant().Trim();
		String[] parameters = pressed.ToString().Split(' ');

		for(int i = 1; i < parameters.Length; i++)
		{//i=0 deliberately ignored as this will simply be "press".
			PressNumber(numButtons.First(b => b.GetComponentInChildren<TextMesh>().text.Equals(parameters[i])));
			yield return null;
		}
	}
}