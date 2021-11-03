using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class SimonShiftsScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    public KMSelectable[] SquareSels;
    public GameObject[] SquareObjs;
    public Material[] SquareColorMats;
    public GameObject StatusLightObj;
    public Light[] SquareLights;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private string[] SOUNDNAMES = { "Sound1", "Sound2", "Sound3", "Sound4", "Sound5" };
    private string[] COLORNAMES = { "Red", "Orange", "Yellow", "Green", "Cyan", "Blue", "Purple", "Magenta" };
    private Color32[] LightColors = new Color32[]
    {
        new Color32(255, 0, 255, 255),
        new Color32(255, 200, 0, 255),
        new Color32(255, 255, 0, 255),
        new Color32(0, 255, 0, 255),
        new Color32(0, 255, 255, 255),
        new Color32(0, 0, 255, 255),
        new Color32(200, 0, 255, 255),
        new Color32(255, 0, 255, 255)
    };
    private int _emptySquare;
    private int[][] _adjacents = new int[9][] { new int[2] { 1, 3 }, new int[3] { 0, 2, 4 }, new int[2] { 1, 5 }, new int[3] { 0, 4, 6 }, new int[4] { 1, 3, 5, 7 }, new int[3] { 2, 4, 8 }, new int[2] { 3, 7 }, new int[3] { 4, 6, 8 }, new int[2] { 5, 7 } };
    private int[] sqColor = new int[9];
    private float[] xPos = { -0.05f, 0f, 0.05f, -0.05f, 0f, 0.05f, -0.05f, 0f, 0.05f };
    private float[] zPos = { 0.05f, 0.05f, 0.05f, 0f, 0f, 0f, -0.05f, -0.05f, -0.05f };
    private Coroutine _moveSquare;
    private bool _isMoving;
    private bool _hasPressed;
    private int[] _flashes;
    private Coroutine _flashSequence;
    private int _stage = 0;
    private List<int> _presses = new List<int>();
    private List<bool> _wasShifted = new List<bool>();
    private int[][] _flashSounds = new int[3][]
    {
        new int[3],
        new int[4],
        new int[5]
    };
    private Coroutine _timer;

    public KMColorblindMode ColorblindMode;
    public TextMesh[] ColorblindText;
    public GameObject[] ColorblindTextObj;
    private bool _colorblindMode;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        for (int i = 0; i < SquareSels.Length; i++)
            SquareSels[i].OnInteract += SquarePress(i);

        _emptySquare = Rnd.Range(0, 9);
        SquareObjs[_emptySquare].SetActive(false);

        foreach (var light in SquareLights)
            light.enabled = false;

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < _flashSounds[i].Length; j++)
                _flashSounds[i][j] = Rnd.Range(0, 5);

        int[] shuffler = Enumerable.Range(0, 8).ToArray().Shuffle();
        int val = 0;
        for (int i = 0; i < 9; i++)
        {
            ColorblindText[i].text = COLORNAMES[sqColor[_emptySquare]].Substring(0, 1);
            if (i != _emptySquare)
            {
                sqColor[i] = shuffler[val];
                SquareLights[i].color = LightColors[shuffler[val]];
                ColorblindText[i].text = COLORNAMES[shuffler[val]].Substring(0, 1);
                SquareObjs[i].GetComponent<MeshRenderer>().material = SquareColorMats[shuffler[val]];
                val++;
            }
            else
            {
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
            if (!_moduleSolved)
            {
                _hasPressed = true;
                if (_flashSequence != null)
                    StopCoroutine(_flashSequence);
                foreach (var light in SquareLights)
                    light.enabled = false;
                //Debug.LogFormat("[Simon Shifts #{0}] Pressed square #{1}, which is {2}.", _moduleId, sq + 1, sqColor[sq] == null ? "EMPTY" : COLORNAMES[(int)sqColor[sq]]);

                if (_adjacents[sq].Contains(_emptySquare) && !_isMoving)
                {
                    _moveSquare = StartCoroutine(MoveSquare(sq));
                    _wasShifted.Add(true);
                }
                else
                    _wasShifted.Add(false);
                if (sq == _emptySquare)
                {
                    Debug.LogFormat("[Simon Shifts #{0}] Pressed the EMPTY square.", _moduleId);
                    CheckAnswer();
                    //_flashSequence = StartCoroutine(FlashSequence());
                }
                else
                {
                    if (_timer != null)
                        StopCoroutine(_timer);
                    _timer = StartCoroutine(Timer());
                    Audio.PlaySoundAtTransform("Press", transform);
                    _presses.Add(sqColor[sq]);
                    //Debug.LogFormat("[Simon Shifts #{0}] Pressed {1}", _moduleId, sqColor[sq] == 8 ? "EMPTY" : COLORNAMES[sqColor[sq]]);
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
        SquareObjs[start].transform.localPosition = new Vector3(xPos[start], 0.01f, zPos[start]);

        SquareObjs[start].SetActive(false);
        SquareObjs[_emptySquare].SetActive(true);

        sqColor[_emptySquare] = sqColor[start];
        sqColor[start] = 8;

        SquareObjs[_emptySquare].GetComponent<MeshRenderer>().material = SquareColorMats[sqColor[_emptySquare]];
        ColorblindText[_emptySquare].text = COLORNAMES[sqColor[_emptySquare]].Substring(0, 1);

        SquareLights[_emptySquare].color = LightColors[sqColor[_emptySquare]];

        _emptySquare = start;
        _isMoving = false;
    }

    private IEnumerator FlashSequence()
    {
        int[] flashes = new int[5];
        for (int i = 0; i < 5; i++)
            flashes[i] = _flashes[i];
        while (true)
        {
            for (int i = 0; i < 3 + _stage; i++)
            {
                if (_hasPressed)
                    Audio.PlaySoundAtTransform(SOUNDNAMES[_flashSounds[_stage][i]], transform);
                SquareLights[Array.IndexOf(sqColor, flashes[i])].enabled = true;
                yield return new WaitForSeconds(0.3f);
                SquareLights[Array.IndexOf(sqColor, flashes[i])].enabled = false;
                yield return new WaitForSeconds(0.12f);
            }
            yield return new WaitForSeconds(1.5f);
        }
    }

    private IEnumerator Timer()
    {
        if (!_moduleSolved)
        {
            yield return new WaitForSeconds(6f);
            _flashSequence = StartCoroutine(FlashSequence());
        }
    }

    private void CheckAnswer()
    {
        bool correct = true;
        for (int i = 0; i < _stage + 3; i++)
        {

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
            _stage++;
            if (_stage == 3)
            {
                _moduleSolved = true;
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
            _flashSequence = StartCoroutine(FlashSequence());
        }
    }

    private IEnumerator SolveAnimation()
    {
        Audio.PlaySoundAtTransform("Solve", transform);
        for (int i = 0; i < 9; i++)
        {
            if (i != 4)
            {
                SquareLights[i].enabled = true;
                yield return new WaitForSeconds(0.45f);
            }
        }
        Module.HandlePass();
    }
}
