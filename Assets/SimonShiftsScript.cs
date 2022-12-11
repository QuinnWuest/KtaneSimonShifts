using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class SimonShiftsScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMColorblindMode ColorblindMode;

    public KMSelectable[] SquareSels;
    public GameObject[] SquareObjs;
    public Material[] SquareColorMats;
    public GameObject StatusLightObj;
    public Light[] SquareLights;
    public TextMesh[] ColorblindText;
    public GameObject[] ColorblindTextObj;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private static readonly string[] SOUNDNAMES = { "Sound1", "Sound2", "Sound3", "Sound4", "Sound5" };
    private static readonly string[] COLORNAMES = { "Red", "Orange", "Yellow", "Green", "Cyan", "Blue", "Purple", "Magenta" };
    private static readonly Color32[] LightColors = new Color32[]
    {
        new Color32(255, 0, 0, 255),
        new Color32(255, 200, 0, 255),
        new Color32(255, 255, 0, 255),
        new Color32(0, 255, 0, 255),
        new Color32(0, 255, 255, 255),
        new Color32(0, 0, 255, 255),
        new Color32(200, 0, 255, 255),
        new Color32(255, 0, 255, 255)
    };
    private int _emptySquare;
    private readonly int[] _sqColor = new int[9];
    private static readonly int[][] _adjacents = new int[9][] { new int[2] { 1, 3 }, new int[3] { 0, 2, 4 }, new int[2] { 1, 5 }, new int[3] { 0, 4, 6 }, new int[4] { 1, 3, 5, 7 }, new int[3] { 2, 4, 8 }, new int[2] { 3, 7 }, new int[3] { 4, 6, 8 }, new int[2] { 5, 7 } };
    private static readonly float[] xPos = { -0.05f, 0f, 0.05f, -0.05f, 0f, 0.05f, -0.05f, 0f, 0.05f };
    private static readonly float[] zPos = { 0.05f, 0.05f, 0.05f, 0f, 0f, 0f, -0.05f, -0.05f, -0.05f };
    private bool _isMoving;
    private bool _isFirstFlash;
    private bool _hasPressed;
    private int[] _flashes;
    private Coroutine _flashSequence;
    private int _stage = 0;
    private readonly List<int> _presses = new List<int>();
    private readonly int[][] _flashSounds = new int[3][]
    {
        new int[3],
        new int[4],
        new int[5]
    };
    private Coroutine _timer;

    private bool _colorblindMode;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        for (int i = 0; i < SquareSels.Length; i++)
            SquareSels[i].OnInteract += SquarePress(i);

        _emptySquare = Rnd.Range(0, 9);
        SquareObjs[_emptySquare].SetActive(false);

        float scalar = transform.lossyScale.x;
        foreach (var light in SquareLights)
        {
            light.range *= scalar;
            light.enabled = false;
        }

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < _flashSounds[i].Length; j++)
                _flashSounds[i][j] = Rnd.Range(0, 5);

        int[] shuffler = Enumerable.Range(0, 8).ToArray().Shuffle();
        int val = 0;
        for (int i = 0; i < 9; i++)
        {
            ColorblindText[i].text = COLORNAMES[_sqColor[i]].Substring(0, 1);
            if (i != _emptySquare)
            {
                _sqColor[i] = shuffler[val];
                SquareLights[i].color = LightColors[shuffler[val]];
                ColorblindText[i].text = COLORNAMES[shuffler[val]].Substring(0, 1);
                SquareObjs[i].GetComponent<MeshRenderer>().material = SquareColorMats[shuffler[val]];
                val++;
            }
            else
            {
                _sqColor[_emptySquare] = 8;
                StatusLightObj.transform.localPosition = new Vector3(xPos[i], 0.01f, zPos[i]);
            }
        }
        _flashes = Enumerable.Range(0, 8).ToArray().Shuffle();
        _flashSequence = StartCoroutine(FlashSequence());
        _colorblindMode = ColorblindMode.ColorblindModeActive;
        SetColorblindMode(_colorblindMode);

        Debug.LogFormat("[Simon Shifts #{0}] Stage 1 flashes are {1} {2} {3}.", _moduleId, COLORNAMES[_flashes[0]], COLORNAMES[_flashes[1]], COLORNAMES[_flashes[2]]);
    }

    private void SetColorblindMode(bool mode)
    {
        for (int i = 0; i < ColorblindText.Length; i++)
            ColorblindTextObj[i].SetActive(mode);
    }

    private KMSelectable.OnInteractHandler SquarePress(int sq)
    {
        return delegate ()
        {
            if (_stage != 3)
            {
                _hasPressed = true;
                if (_flashSequence != null)
                    StopCoroutine(_flashSequence);
                if (_timer != null)
                    StopCoroutine(_timer);
                foreach (var light in SquareLights)
                    light.enabled = false;

                if (sq == _emptySquare)
                {
                    Debug.LogFormat("[Simon Shifts #{0}] Pressed the EMPTY square.", _moduleId);
                    CheckAnswer();
                    //_flashSequence = StartCoroutine(FlashSequence());
                }
                else
                {
                    _timer = StartCoroutine(Timer());
                    Audio.PlaySoundAtTransform("Press", transform);
                    if (_adjacents[sq].Contains(_emptySquare) && !_isMoving)
                    {
                        _presses.Add(_sqColor[sq]);
                        while (_presses.Count > _stage + 3)
                            _presses.RemoveAt(0);
                        StartCoroutine(MoveSquare(sq));
                    }
                }
            }
            return false;
        };
    }

    private IEnumerator MoveSquare(int start)
    {
        _isMoving = true;
        var duration = 0.1f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            SquareObjs[start].transform.localPosition = new Vector3(Easing.InOutQuad(elapsed, xPos[start], xPos[_emptySquare], duration), 0.01f, Easing.InOutQuad(elapsed, zPos[start], zPos[_emptySquare], duration));
            StatusLightObj.transform.localPosition = new Vector3(Easing.InOutQuad(elapsed, xPos[_emptySquare], xPos[start], duration), 0.01f, Easing.InOutQuad(elapsed, zPos[_emptySquare], zPos[start], duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        StatusLightObj.transform.localPosition = new Vector3(xPos[start], 0.01f, zPos[start]);
        SquareObjs[start].transform.localPosition = new Vector3(xPos[start], 0.01f, zPos[start]);

        SquareObjs[start].SetActive(false);
        SquareObjs[_emptySquare].SetActive(true);

        _sqColor[_emptySquare] = _sqColor[start];
        _sqColor[start] = 8;

        SquareObjs[_emptySquare].GetComponent<MeshRenderer>().material = SquareColorMats[_sqColor[_emptySquare]];
        ColorblindText[_emptySquare].text = COLORNAMES[_sqColor[_emptySquare]].Substring(0, 1);

        SquareLights[_emptySquare].color = LightColors[_sqColor[_emptySquare]];

        _emptySquare = start;
        _isMoving = false;
    }

    private IEnumerator FlashSequence()
    {
        int[] flashes = new int[5];
        for (int i = 0; i < 5; i++)
            flashes[i] = _flashes[i];
        _isFirstFlash = true;
        while (true)
        {
            for (int i = 0; i < 3 + _stage; i++)
            {
                if (_hasPressed)
                    Audio.PlaySoundAtTransform(SOUNDNAMES[_flashSounds[_stage][i]], transform);
                SquareLights[Array.IndexOf(_sqColor, flashes[i])].enabled = true;
                yield return new WaitForSeconds(0.3f);
                SquareLights[Array.IndexOf(_sqColor, flashes[i])].enabled = false;
                yield return new WaitForSeconds(0.12f);
            }
            _isFirstFlash = false;
            yield return new WaitForSeconds(1.5f);
        }
    }

    private IEnumerator Timer()
    {
        if (_stage != 3)
        {
            yield return new WaitForSeconds(4f);
            _flashSequence = StartCoroutine(FlashSequence());
        }
    }

    private void CheckAnswer()
    {
        bool correct = true;
        for (int i = 0; i < _stage + 3; i++)
        {
            if (_sqColor[4] != 8)
            {
                correct = false;
                break;
            }
            if (_presses.Count < _stage + 3)
            {
                correct = false;
                break;
            }
            if (_presses[_presses.Count - (_stage + 3) + i] != _flashes[i])
                correct = false;
        }
        if (correct)
        {
            if (_flashSequence != null)
                StopCoroutine(_flashSequence);
            _stage++;
            if (_stage == 3)
            {
                Debug.LogFormat("[Simon Shifts #{0}] Successfully completed Stage {1}. Module solved.", _moduleId, _stage);
                StartCoroutine(SolveAnimation());
            }
            else
            {
                Debug.LogFormat("[Simon Shifts #{0}] Successfully completed Stage {1}.", _moduleId, _stage);
                _flashes = Enumerable.Range(0, 8).ToArray().Shuffle();
                _flashSequence = StartCoroutine(FlashSequence());
                Debug.LogFormat("[Simon Shifts #{0}] Stage {6} flashes are {1} {2} {3} {4}{5}.", _moduleId, COLORNAMES[_flashes[0]], COLORNAMES[_flashes[1]], COLORNAMES[_flashes[2]], COLORNAMES[_flashes[3]], _stage == 2 ? " " + COLORNAMES[_flashes[4]] : "", _stage + 1);
            }
        }
        else
        {
            Module.HandleStrike();
            if (_flashSequence != null)
                StopCoroutine(_flashSequence);
            _flashSequence = StartCoroutine(FlashSequence());
        }
    }

    private IEnumerator SolveAnimation()
    {
        Audio.PlaySoundAtTransform("Solve", transform);
        if (_timer != null)
            StopCoroutine(_timer);
        for (int i = 0; i < 9; i++)
        {
            if (i != 4)
            {
                SquareLights[i].enabled = true;
                yield return new WaitForSeconds(0.45f);
            }
        }
        _moduleSolved = true;
        Module.HandlePass();
    }

    private static readonly string tpColors = "ROYGCBPMroygcbpm";

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} ROYGCBPM: Press red, orange, yellow, green, cyan, blue, purple, magenta | !{0} submit: Presses the status light to submit that stage. | !{0} colorblind";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        var m = Regex.Match(command, @"^\s*([roygcbpm ]+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            foreach (var ch in m.Groups[1].Value)
            {
                var ix = tpColors.IndexOf(ch) % 8;
                if (ix != -1)
                {
                    SquareSels[Array.IndexOf(_sqColor, ix)].OnInteract();
                    yield return new WaitForSeconds(0.2f);
                }
            }
            yield break;
        }
        m = Regex.Match(command, @"^\s*(submit|sl|status light)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            yield return "strike";
            yield return "solve";
            SquareSels[Array.IndexOf(_sqColor, 8)].OnInteract();
            yield return new WaitForSeconds(0.2f);
            yield break;
        }
        m = Regex.Match(command, @"^\s*colou?rblind\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            _colorblindMode = !_colorblindMode;
            SetColorblindMode(_colorblindMode);
            yield break;
        }
    }

    private struct SolverQueueItem : IEquatable<SolverQueueItem>
    {
        public int[] SqColors;
        public int[] Presses;   // Square positions of all presses

        public bool Equals(SolverQueueItem other)
        {
            return other.SqColors.SequenceEqual(SqColors);
        }
        public override int GetHashCode()
        {
            return ArrayHash(SqColors);
        }
        public override bool Equals(object obj)
        {
            return obj is SolverQueueItem && Equals((SolverQueueItem) obj);
        }

        /// <summary>
        ///     Computes a hash value from an array of elements.</summary>
        /// <param name="input">
        ///     The array of elements to hash.</param>
        /// <returns>
        ///     The computed hash value.</returns>
        private static int ArrayHash(Array input)
        {
            if (input == null)
                return 0;

            const int b = 378551;
            int a = 63689;
            int hash = input.Length + 1;

            unchecked
            {
                foreach (object t in input)
                {
                    if (t is Array)
                        hash = hash * a + ArrayHash((Array) t);
                    else if (t != null)
                        hash = hash * a + t.GetHashCode();
                    a *= b;
                }
            }

            return hash;
        }
    }

    private static int[][] _fillOrders = new[]
    {
        new[] { 4, 1, 0, 3, 6, 7 },
        new[] { 4, 1, 2, 5, 8, 7 },
        new[] { 4, 7, 6, 3, 0, 1 },
        new[] { 4, 7, 8, 5, 2, 1 },
        new[] { 4, 5, 8, 7, 6, 3 },
        new[] { 4, 5, 2, 1, 0, 3 },
        new[] { 4, 3, 0, 1, 2, 5 },
        new[] { 4, 3, 6, 7, 8, 5 }
    };

    IEnumerator TwitchHandleForcedSolve()
    {
        while (!_moduleSolved)
        {
            if (_stage == 3 || _isMoving)
            {
                yield return true;
                continue;
            }

            var numFlashes = _stage + 3;

            var goals = _fillOrders.Select(fillOrder =>
            {
                var goal = new int?[9];
                for (var i = 0; i < numFlashes + 1; i++)
                    goal[fillOrder[i]] = i == numFlashes ? 8 : _flashes[numFlashes - 1 - i];
                return goal;
            }).ToArray();

            var q = new Queue<SolverQueueItem>();
            var already = new HashSet<SolverQueueItem>();
            var currentState = new SolverQueueItem { SqColors = _sqColor.ToArray(), Presses = new int[0] };
            q.Enqueue(currentState);
            while (q.Count > 0)
            {
                var item = q.Dequeue();
                if (!already.Add(item))
                    continue;
                int goalIx = goals.IndexOf(goal => Enumerable.Range(0, 9).All(ix => goal[ix] == null || item.SqColors[ix] == goal[ix].Value));
                if (goalIx != -1)
                {
                    // Solution found
                    foreach (var pr in item.Presses)
                    {
                        SquareSels[pr].OnInteract();
                        while (_isMoving)
                            yield return true;
                    }
                    for (var i = numFlashes - 1; i >= 0; i--)
                    {
                        SquareSels[_fillOrders[goalIx][i]].OnInteract();
                        while (_isMoving)
                            yield return true;
                    }
                    SquareSels[4].OnInteract();
                    yield return new WaitForSeconds(.1f);
                    while (_isFirstFlash)
                        yield return true;
                    goto nextStage;
                }

                var blank = Array.IndexOf(item.SqColors, 8);
                foreach (var adj in _adjacents[blank])
                {
                    var newSqColors = item.SqColors.ToArray();
                    newSqColors[blank] = item.SqColors[adj];
                    newSqColors[adj] = 8;
                    var newPresses = new int[item.Presses.Length + 1];
                    Array.Copy(item.Presses, newPresses, item.Presses.Length);
                    newPresses[newPresses.Length - 1] = adj;
                    var newItem = new SolverQueueItem { SqColors = newSqColors, Presses = newPresses };
                    if (!already.Contains(newItem))
                        q.Enqueue(newItem);
                }
            }

            nextStage:;
        }
    }
}
