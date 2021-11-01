using System;
using System.Collections;
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

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private int _emptySquare;
    private int[][] _adjacents = new int[9][]
    {
        new int[2] { 1, 3 },
        new int[3] { 0, 2, 4 },
        new int[2] { 1, 5},
        new int[3] { 0, 4, 6 },
        new int[4] { 1, 3, 5, 7 },
        new int[3] { 2, 4, 8 },
        new int[2] { 3, 7 },
        new int[3] { 4, 6, 8 },
        new int[2] { 5, 7 }
    };
    private float[] xPos = { -0.05f, 0f, 0.05f, -0.05f, 0f, 0.05f, -0.05f, 0f, 0.05f };
    private float[] zPos = { 0.05f, 0.05f, 0.05f, 0f, 0f, 0f, -0.05f, -0.05f, -0.05f };
    private Coroutine _moveSquare;
    private bool _isMoving;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        for (int i = 0; i < SquareSels.Length; i++)
            SquareSels[i].OnInteract += SquarePress(i);

        _emptySquare = Rnd.Range(0, 9);
        SquareObjs[_emptySquare].SetActive(false);
        int val = 0;
        for (int i = 0; i < 8; i++)
        {
            if (i != _emptySquare)
            {
                SquareObjs[i].GetComponent<MeshRenderer>().material = SquareColorMats[val];
                val++;
            }
        }
    }

    private KMSelectable.OnInteractHandler SquarePress(int sq)
    {
        return delegate ()
        {
            Debug.LogFormat("[Simon Shifts #{0}] Pressed square #{1}", _moduleId, sq + 1);
            if (_adjacents[sq].Contains(_emptySquare) && !_isMoving)
                _moveSquare = StartCoroutine(MoveSquare(sq, _emptySquare));
            return false;
        };
    }

    private IEnumerator MoveSquare(int start, int end)
    {
        _isMoving = true;
        var duration = 0.15f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            SquareObjs[start].transform.localPosition = new Vector3(Easing.InOutQuad(elapsed, xPos[start], xPos[end], duration), 0.01f, Easing.InOutQuad(elapsed, zPos[start], zPos[end], duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        SquareObjs[start].transform.localPosition = new Vector3(xPos[start], 0.01f, zPos[start]);
        SquareObjs[start].SetActive(false);
        SquareObjs[_emptySquare].SetActive(true);
        SquareObjs[_emptySquare].GetComponent<MeshRenderer>().material = SquareObjs[start].GetComponent<MeshRenderer>().material;
        _emptySquare = start;
        _isMoving = false;
    }
}
