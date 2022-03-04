using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;
using System.Text.RegularExpressions;

public class GhostScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public AudioSource GhostMusic;

    public Material GhostHLMat;
    public Material GhostShellMat;
    public GameObject SpikeObj;
    public KMSelectable[] LedSels;
    public GameObject[] LedObjs;
    public Material[] LedMats;
    public GameObject CrosshairObj;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private int _ghostId;
    private static int _ghostIdCounter = 1;
    private bool _moduleSolved;
    private float defaultGameMusicVolume;
    private bool TwitchPlaysActive;
    private bool _twitchMode;
    private IDictionary<string, object> tpAPI;

    // Large Ghost
    public GameObject GhostLargeParent;
    public GameObject GhostLargeObj;
    public GameObject GhostLargeShell;
    public GameObject GhostLargeEyes;
    public KMSelectable GhostLargeSel;
    public Material LargeHitMat;
    public Material LargeHitEyesMat;
    public Material GhostLargeEyesMat;
    public Material GhostMediumEyesMat;

    private bool _isLargeHit;
    private int _largeHitCount;
    private bool _canPressLarge = true;
    private bool[] _largePressRules = new bool[4];
    private bool _canAttack;

    // Medium Ghost
    public GameObject GhostMediumBigParent;
    public GameObject[] GhostMediumParent;
    public GameObject[] GhostMediumObj;
    public GameObject[] GhostMediumShell;
    public GameObject[] GhostMediumEyes;
    public KMSelectable[] GhostMediumSel;
    public Material MediumHitMat;
    public Material MediumHitEyesMat;

    private bool[] _isMediumHit = new bool[4];
    private bool[] _canPressMedium = new bool[4] { true, true, true, true };

    // Small Ghost
    public GameObject[] GhostSmallParent;
    public GameObject[] GhostSmallObj;
    public GameObject[] GhostSmallShell;
    public GameObject[] GhostSmallIndivParent;
    public KMSelectable[] GhostSmallSel;
    private bool[] _canPressSmall = new bool[16] { true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true };

    // Everything else
    private int _bombTimeSeconds;
    private int _tensDigit;
    private int _onesDigit;
    private int _currentPhase;
    private static readonly float[][] _spikeXPos = new float[3][]
    {
        new float[1] {0.0f},
        new float[4] {-0.04f, 0.04f,-0.04f, 0.04f},
        new float[4] {-0.06f, -0.02f, 0.02f, 0.06f}
    };
    private static readonly float[] _spikeYPos = new float[3] { 0.04f, 0.03f, 0.03f };
    private static readonly float[][] _spikeZPos = new float[3][]
    {
        new float[1] {-0.007f},
        new float[4] {0.03f, -0.02f, 0.03f, -0.02f},
        new float[4] {0.04f, 0.01f, -0.02f, -0.05f}
    };
    private static readonly float[] _ledXPos = new float[3] { -0.04f, 0.0f, 0.04f };
    private Coroutine _moveSpike;
    private int _ledPosition = 0;
    private int[] _snCharacters = new int[6];
    private int[] _codeSets = new int[4];
    private int _phaseTwoPressIx;
    private int[][] _toPress = new int[4][]
    {
        new int[4]{4, 4, 4, 4},
        new int[4]{4, 4, 4, 4},
        new int[4]{4, 4, 4, 4},
        new int[4]{4, 4, 4, 4}
    };
    private int[] _phaseTwoPressOrder = new int[16];
    private int[] _phaseThreePressOrder = new int[16];
    private int _phaseThreePressIx;
    private int _spikeTarget;
    private int[] _lazyArr = new int[] { 0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15 };

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        _ghostId = _ghostIdCounter++;
        try
        {
            if (_ghostId == 1)
                defaultGameMusicVolume = GameMusicControl.GameMusicVolume;
        }
        catch (Exception) { }

        GhostLargeSel.OnInteract += GhostLargeSelPress;
        GhostLargeSel.OnHighlight += delegate () { GhostHighlight(GhostLargeShell); };
        GhostLargeSel.OnHighlightEnded += delegate () { GhostHighlightEnd(GhostLargeShell); };

        for (int i = 0; i < GhostMediumSel.Length; i++)
        {
            GhostMediumSel[i].OnInteract += GhostMediumSelPress(i);
            int j = i;
            GhostMediumSel[i].OnHighlight += delegate ()
            {
                if (!_isMediumHit[j])
                    GhostHighlight(GhostMediumShell[j]);
            };
            GhostMediumSel[i].OnHighlightEnded += delegate ()
            {
                if (!_isMediumHit[j])
                    GhostHighlightEnd(GhostMediumShell[j]);
            };
        }

        for (int i = 0; i < GhostSmallSel.Length; i++)
        {
            GhostSmallSel[i].OnInteract += GhostSmallSelPress(i);
            int j = i;
            GhostSmallSel[i].OnHighlight += delegate () { GhostHighlight(GhostSmallShell[j]); };
            GhostSmallSel[i].OnHighlightEnded += delegate () { GhostHighlightEnd(GhostSmallShell[j]); };
        }
        for (int i = 0; i < LedSels.Length; i++)
            LedSels[i].OnInteract += LedPress(i);

        StartCoroutine(SpinSpike());
        StartCoroutine(AttackHandler());
        StartCoroutine(AnimateGhost(GhostLargeObj));
        for (int i = 0; i < 4; i++)
            StartCoroutine(AnimateGhost(GhostMediumObj[i]));
        for (int i = 0; i < 16; i++)
            StartCoroutine(AnimateGhost(GhostSmallObj[i]));

        _snCharacters = BombInfo.GetSerialNumber().ToArray().Select(i => i >= 'A' ? i - 'A' + 1 : i - '0').ToArray();
        _codeSets[0] = _snCharacters[0] * _snCharacters[1];
        _codeSets[1] = _snCharacters[2] * _snCharacters[3];
        _codeSets[2] = _snCharacters[4] * _snCharacters[5];
        _codeSets[3] = _snCharacters[0] + _snCharacters[1] + _snCharacters[2] + _snCharacters[3] + _snCharacters[4] + _snCharacters[5];

        for (int j = 0; j < 4; j++)
        {
            var listOfNums = new List<int>() { 0, 1, 2, 3 };
            _toPress[j] = new int[4] { 4, 4, 4, 4 };
            int num = _codeSets[j];
            for (int i = 0; i < 4; i++)
            {
                var toAdd = num % (4 - i);
                _toPress[j][i] = listOfNums[toAdd];
                listOfNums.RemoveAt(toAdd);
                num /= (4 - i);
            }
        }
        var p2List = new List<int>();
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                p2List.Add(_toPress[i][j]);
        _phaseTwoPressOrder = p2List.ToArray();
        Debug.LogFormat("[Ghost #{0}] Phase Two: The order to hit the blobs is {1}.", _moduleId, _phaseTwoPressOrder.Select(i => "ABCD".Substring(i, 1)).Join(""));
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                _phaseThreePressOrder[(i * 4) + j] = _lazyArr[_toPress[j][i] + (j * 4)];
        Debug.LogFormat("[Ghost #{0}] Phase Three: The order to hit the blobs is {1}.", _moduleId, _phaseThreePressOrder.Select(i => ConvertToCoord(i)).Join(" "));

        Module.OnActivate += Activate;
        BombInfo.OnBombExploded += delegate ()
        {
            _ghostIdCounter = 1;
            if (_ghostId == 1 && GhostMusic.isPlaying)
            {
                GhostMusic.Stop();
                try { GameMusicControl.GameMusicVolume = defaultGameMusicVolume; }
                catch (Exception) { }
            }
        };
    }

    private string ConvertToCoord(int num)
    {
        return "ABCD".Substring(num / 4, 1) + "1234".Substring(num % 4, 1);
    }

    private void Update()
    {
        if (_ghostIdCounter == 1 && GhostMusic.isPlaying && _moduleSolved)
        {
            GhostMusic.Stop();
            try { GameMusicControl.GameMusicVolume = defaultGameMusicVolume; }
            catch (Exception) { }
        }
        _bombTimeSeconds = (int)BombInfo.GetTime() % 60;
        _tensDigit = _bombTimeSeconds / 10;
        _onesDigit = _bombTimeSeconds % 10;
        if (!_canAttack)
        {
            var solveCount = BombInfo.GetSolvedModuleNames().Count();
            if (solveCount != 0)
                _canAttack = true;
        }
    }

    private void Activate()
    {
        var isTherePow = BombInfo.GetModuleNames().ToArray().Contains("Pow"); // Give music priority to Pow in case both a Ghost and Pow appear on a bomb.
        if (_ghostId == 1 && !isTherePow)
        {
            GhostMusic.Play();
            try { GameMusicControl.GameMusicVolume = 0.0f; }
            catch (Exception) { }
        }
        if (TwitchPlaysActive)
        {
            _twitchMode = true;
            GameObject tpAPIGameObject = GameObject.Find("TwitchPlays_Info");
            if (tpAPIGameObject != null)
                tpAPI = tpAPIGameObject.GetComponent<IDictionary<string, object>>();
            else
                _twitchMode = false;
        }
    }

    private bool GhostLargeSelPress()
    {
        Audio.PlaySoundAtTransform("Hit", transform);
        if (_moduleSolved)
            return false;
        _canAttack = true;
        if (!_canPressLarge)
            return false;

        if ((int)BombInfo.GetTime() <= 60)
        {
            Debug.LogFormat("[Ghost #{0}] Skipped the press rules, as there are less than 60 seconds left on the timer.", _moduleId);
            goto correct;
        }
        if (_tensDigit != _onesDigit)
        {
            if (_tensDigit % 2 == 0 && _onesDigit % 2 == 0 && !_largePressRules[0])
            {
                Debug.LogFormat("[Ghost #{0}] Applied rule 1 while pressing the large blob. (When both seconds digits of the timer are even and different)", _moduleId);
                _largePressRules[0] = true;
                goto correct;
            }
            if (_tensDigit % 2 == 1 && _onesDigit % 2 == 1 && !_largePressRules[1])
            {
                Debug.LogFormat("[Ghost #{0}] Applied rule 2 while pressing the large blob. (When both seconds digits of the timer are odd and different)", _moduleId);
                _largePressRules[1] = true;
                goto correct;
            }
            if (!_largePressRules[3])
            {
                Debug.LogFormat("[Ghost #{0}] Applied rule 4 while pressing the large blob. (At any time)", _moduleId);
                _largePressRules[3] = true;
                goto correct;
            }
            goto wrong;
        }
        else
        {
            if (_tensDigit == _onesDigit && !_largePressRules[2])
            {
                Debug.LogFormat("[Ghost #{0}] Applied rule 3 while pressing the large blob. (When the seconds digits of the timer are the same)", _moduleId);
                _largePressRules[2] = true;
                goto correct;
            }
            if (!_largePressRules[3])
            {
                Debug.LogFormat("[Ghost #{0}] Applied rule 4 while pressing the large blob. (At any time)", _moduleId);
                _largePressRules[3] = true;
                goto correct;
            }
            goto wrong;
        }

        wrong:
        Module.HandleStrike();
        _largeHitCount = 0;
        for (int i = 0; i < _largePressRules.Length; i++)
            _largePressRules[i] = false;
        Debug.LogFormat("[Ghost #{0}] You didn't follow each rule exactly once, or pressed the blob at the wrong time. Strike.", _moduleId);
        goto finished;

        correct:
        _largeHitCount++;
        StartCoroutine(LargeHitAnimation());
        if (_largeHitCount == 4)
        {
            Debug.LogFormat("[Ghost #{0}] Successfully followed each rule once, completing Phase One. Moving to Phase Two.", _moduleId);
            _currentPhase = 1;
            StartCoroutine(ShrinkLarge());
            _canPressLarge = false;
        }
        finished:;
        return false;
    }

    private KMSelectable.OnInteractHandler GhostMediumSelPress(int index)
    {
        return delegate ()
        {
            if (_moduleSolved)
                return false;
            Audio.PlaySoundAtTransform("Hit", transform);
            if (!_canPressMedium[index])
                return false;
            if (index == _phaseTwoPressOrder[_phaseTwoPressIx])
            {
                _phaseTwoPressIx++;
                StartCoroutine(MediumHitAnimation(index));
            }
            else
            {
                Debug.LogFormat("[Ghost #{0}] Phase Two: Pressed blob {1} when blob {2} was expected. Strike.", _moduleId, index, _phaseTwoPressOrder[_phaseTwoPressIx]);
                Module.HandleStrike();
                _phaseTwoPressIx = 0;
            }
            if (_phaseTwoPressIx == 16)
            {
                Debug.LogFormat("[Ghost #{0}] Successfully pressed the blobs in the correct order, completing Phase Two. Moving to Phase Three.", _moduleId);
                Audio.PlaySoundAtTransform("Split", transform);
                _currentPhase = 2;
                for (int i = 0; i < 4; i++)
                {
                    _canPressMedium[i] = false;
                    StartCoroutine(ShrinkMedium(i));
                }
            }
            return false;
        };
    }

    private KMSelectable.OnInteractHandler GhostSmallSelPress(int index)
    {
        return delegate ()
        {
            if (_moduleSolved)
                return false;
            Audio.PlaySoundAtTransform("Hit", transform);
            if (!_canPressSmall[index])
                return false;
            if (index == _lazyArr[_phaseThreePressOrder[_phaseThreePressIx]])
            {
                _canPressSmall[index] = false;
                StartCoroutine(ShrinkSmall(index));
                _phaseThreePressIx++;
            }
            else
            {
                Debug.LogFormat("[Ghost #{0}] Phase Three: Pressed blob {1} when blob {2} was expected. Strike.", _moduleId, ConvertToCoord(_lazyArr[index]), ConvertToCoord(_phaseThreePressOrder[_phaseThreePressIx]));
                Module.HandleStrike();
            }
            if (_phaseThreePressIx == 16)
                StartCoroutine(SolveModule());
            return false;
        };
    }

    private IEnumerator SolveModule()
    {
        _moduleSolved = true;
        _ghostIdCounter--;
        if (_moveSpike != null)
            StopCoroutine(_moveSpike);
        SpikeObj.SetActive(false);
        CrosshairObj.SetActive(false);
        yield return new WaitForSeconds(0.3f);
        Module.HandlePass();
        Debug.LogFormat("[Ghost #{0}] Successfully completed Phase Three. Module solved.", _moduleId);
    }

    private void GhostHighlight(GameObject obj)
    {
        if (_isLargeHit)
            return;
        obj.GetComponent<MeshRenderer>().material = GhostHLMat;
    }

    private void GhostHighlightEnd(GameObject obj)
    {
        if (_isLargeHit)
            return;
        obj.GetComponent<MeshRenderer>().material = GhostShellMat;
    }

    private KMSelectable.OnInteractHandler LedPress(int index)
    {
        return delegate ()
        {
            _ledPosition = index;
            for (int i = 0; i < 3; i++)
            {
                if (i == _ledPosition)
                    LedObjs[i].GetComponent<MeshRenderer>().material = LedMats[1];
                else
                    LedObjs[i].GetComponent<MeshRenderer>().material = LedMats[0];
            }
            return false;
        };
    }

    private IEnumerator LargeHitAnimation()
    {
        _isLargeHit = true;
        GhostLargeShell.GetComponent<MeshRenderer>().material = LargeHitMat;
        GhostLargeEyes.GetComponent<MeshRenderer>().material = LargeHitEyesMat;
        yield return new WaitForSeconds(0.7f);
        GhostLargeShell.GetComponent<MeshRenderer>().material = GhostShellMat;
        GhostLargeEyes.GetComponent<MeshRenderer>().material = GhostLargeEyesMat;
        _isLargeHit = false;
    }

    private IEnumerator MediumHitAnimation(int index)
    {
        _isMediumHit[index] = true;
        GhostMediumShell[index].GetComponent<MeshRenderer>().material = MediumHitMat;
        GhostMediumEyes[index].GetComponent<MeshRenderer>().material = MediumHitEyesMat;
        yield return new WaitForSeconds(0.7f);
        GhostMediumShell[index].GetComponent<MeshRenderer>().material = GhostShellMat;
        GhostMediumEyes[index].GetComponent<MeshRenderer>().material = GhostMediumEyesMat;
        _isMediumHit[index] = false;
    }

    private IEnumerator AnimateGhost(GameObject obj)
    {
        while (true)
        {
            var duration = 0.3f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                obj.transform.localScale = new Vector3(Easing.InOutQuad(elapsed, 1.1f, 0.9f, duration), 1f, Easing.InOutQuad(elapsed, 0.9f, 1.1f, duration));
                yield return null;
                elapsed += Time.deltaTime;
            }
            obj.transform.localScale = new Vector3(0.9f, 1f, 1.1f);
            elapsed = 0f;
            while (elapsed < duration)
            {
                obj.transform.localScale = new Vector3(Easing.InOutQuad(elapsed, 0.9f, 1.1f, duration), 1f, Easing.InOutQuad(elapsed, 1.1f, 0.9f, duration));
                yield return null;
                elapsed += Time.deltaTime;
            }
            obj.transform.localScale = new Vector3(1.1f, 1f, 0.9f);
            yield return new WaitForSeconds(.1f);
        }
    }

    private IEnumerator ShrinkLarge()
    {
        Audio.PlaySoundAtTransform("Split", transform);
        var duration = 0.3f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            GhostLargeParent.transform.localScale = new Vector3(Easing.InOutQuad(elapsed, 1.5f, 0f, duration), Easing.InOutQuad(elapsed, 1.5f, 0f, duration), Easing.InOutQuad(elapsed, 1.5f, 0f, duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        GhostLargeParent.transform.localScale = new Vector3(0, 0, 0);
        StartCoroutine(GrowMedium());
    }

    private IEnumerator ShrinkMedium(int objNum)
    {
        var duration = 0.3f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            GhostMediumParent[objNum].transform.localScale = new Vector3(Easing.InOutQuad(elapsed, 0.7f, 0f, duration), Easing.InOutQuad(elapsed, 0.7f, 0f, duration), Easing.InOutQuad(elapsed, 0.7f, 0f, duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        GhostMediumParent[objNum].transform.localScale = new Vector3(0, 0, 0);
        for (int i = 0; i < 4; i++)
        {
            StartCoroutine(GrowSmall(objNum));
        }
    }

    private IEnumerator ShrinkSmall(int objNum)
    {
        _canPressSmall[objNum] = false;
        var duration = 0.3f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            GhostSmallIndivParent[objNum].transform.localScale = new Vector3(Easing.InOutQuad(elapsed, 0.4f, 0f, duration), Easing.InOutQuad(elapsed, 0.4f, 0f, duration), Easing.InOutQuad(elapsed, 0.4f, 0f, duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        GhostSmallIndivParent[objNum].transform.localScale = new Vector3(0f, 0f, 0f);
    }

    private IEnumerator GrowMedium()
    {
        var duration = 0.3f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            GhostMediumBigParent.transform.localScale = new Vector3(Easing.InOutQuad(elapsed, 0f, 1f, duration), Easing.InOutQuad(elapsed, 0f, 1f, duration), Easing.InOutQuad(elapsed, 0f, 1f, duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        GhostMediumBigParent.transform.localScale = new Vector3(1f, 1f, 1f);
    }

    private IEnumerator GrowSmall(int index)
    {
        var duration = 0.3f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            GhostSmallParent[index].transform.localScale = new Vector3(Easing.InOutQuad(elapsed, 0f, 1f, duration), Easing.InOutQuad(elapsed, 0f, 1f, duration), Easing.InOutQuad(elapsed, 0f, 1f, duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        GhostSmallParent[index].transform.localScale = new Vector3(1f, 1f, 1f);
    }

    private IEnumerator SpinSpike()
    {
        while (true)
        {
            var duration = 0.5f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                SpikeObj.transform.localEulerAngles = new Vector3(0f, Mathf.Lerp(0f, 360f, elapsed / duration), 0f);
                yield return null;
                elapsed += Time.deltaTime;
            }
        }
    }

    private IEnumerator AttackHandler()
    {
        while (!_moduleSolved)
        {
            while (!_canAttack)
                yield return null;
            float waitTime = 0f;
            if (_currentPhase == 0)
                waitTime = Rnd.Range(10.0f, 15.0f);
            if (_currentPhase == 1)
                waitTime = Rnd.Range(7.5f, 10.0f);
            if (_currentPhase == 2)
                waitTime = Rnd.Range(5.0f, 10.0f);
            if (_twitchMode)
                waitTime *= 3f;
            yield return new WaitForSeconds(waitTime);
            if (_moduleSolved)
                yield break;
            _moveSpike = StartCoroutine(MoveSpike());
            yield return new WaitForSeconds(3f);
        }
    }

    private IEnumerator MoveSpike()
    {
        SpikeObj.SetActive(true);
        CrosshairObj.SetActive(true);
        _spikeTarget = Rnd.Range(0, 3);
        var duration = 4f;
        CrosshairObj.transform.localPosition = new Vector3(_ledXPos[_spikeTarget], 0.0151f, -0.0575f);
        if (_twitchMode)
        {
            tpAPI["ircConnectionSendMessage"] = "LED " + (_spikeTarget + 1) + " is about to be attacked on Module " + GetModuleCode() + " (Ghost)!";
            duration = 15f;
        }
        var elapsed = 0f;
        var spikeStart = Rnd.Range(0, _spikeXPos[_currentPhase].Length);
        Audio.PlaySoundAtTransform("Attack", transform);
        while (elapsed < duration)
        {
            SpikeObj.transform.localPosition = new Vector3(
                Mathf.Lerp(_spikeXPos[_currentPhase][spikeStart], _ledXPos[_spikeTarget], elapsed / duration),
                Mathf.Lerp(_spikeYPos[_currentPhase], 0.017f, elapsed / duration),
                Mathf.Lerp(_spikeZPos[_currentPhase][spikeStart], -0.06f, elapsed / duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        SpikeObj.SetActive(false);
        CrosshairObj.SetActive(false);
        AttackLED();
    }

    private void AttackLED()
    {
        if (_moduleSolved)
            return;
        if (_spikeTarget == _ledPosition)
        {
            Module.HandleStrike();
            Debug.LogFormat("[Ghost #{0}] An attack was made onto LED {1} that wasn't avoided. Strike.", _moduleId, _spikeTarget + 1);
        }
    }

    private string GetModuleCode()
    {
        Transform closest = null;
        float closestDistance = float.MaxValue;
        foreach (Transform children in transform.parent)
        {
            var distance = (transform.position - children.position).magnitude;
            if (children.gameObject.name == "TwitchModule(Clone)" && (closest == null || distance < closestDistance))
            {
                closest = children;
                closestDistance = distance;
            }
        }
        return closest != null ? closest.Find("MultiDeckerUI").Find("IDText").GetComponent<UnityEngine.UI.Text>().text : null;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = "LEDs: !{0} led 1-3 [Presses LED 1-3]\nPhase 1: !{0} press at xx yy [Press when the timer's seconds digits are xx and yy.]\nPhase 2: !{0} press A B C D [Press blobs A B C D.]\nPhase 3: !{0} press A1 B2 C3 D4 [Press blobs A1, B2, C3, D4.]";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        var parameters = command.Split(' ');
        if (Regex.IsMatch(parameters[0], @"^\s*led\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            if (parameters.Length > 2)
                yield return "sendtochaterror Too many parameters!";
            else if (parameters.Length == 2)
            {
                int temp = 0;
                if (int.TryParse(parameters[1], out temp))
                {
                    if (temp < 1 || temp > 3)
                    {
                        yield return "sendtochaterror The specified LED to press '" + parameters[1] + "' is out of range 1-3!";
                        yield break;
                    }
                    LedSels[temp - 1].OnInteract();
                    yield break;
                }
                else
                    yield return "sendtochaterror The specified LED to press '" + parameters[1] + "' is invalid!";
            }
            else if (parameters.Length == 1)
                yield return "sendtochaterror Please specify the LED to press!";
            yield break;
        }
        if (Regex.IsMatch(parameters[0], @"^\s*press\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            if (_currentPhase == 0)
            {
                if (Regex.IsMatch(parameters[1], @"^\s*at\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    var pressList = new List<int>();
                    for (int i = 2; i < parameters.Length; i++)
                    {
                        int temp = 0;
                        if (!int.TryParse(parameters[i], out temp))
                        {
                            yield return "sendtochaterror The given time to press '" + parameters[i] + "' is invalid!";
                            yield break;
                        }
                        else if (temp < 0 || temp > 59)
                        {
                            yield return "sendtochaterror The given time to press '" + parameters[i] + "' is out of range 0-59!";
                            yield break;
                        }
                        pressList.Add(temp);
                        if (pressList.Count > 4)
                        {
                            yield return "sendtochaterror More than four times to press were given!";
                            yield break;
                        }
                    }
                    yield return "multiple strikes";
                    while (pressList.Count > 0)
                    {
                        keepWaiting:
                        var ix = pressList.IndexOf((int)BombInfo.GetTime() % 60);
                        if (ix == -1)
                        {
                            yield return "trycancel";
                            goto keepWaiting;
                        }
                        GhostLargeSel.OnInteract();
                        yield return new WaitForSeconds(0.05f);
                        if (_largeHitCount == 0)
                        {
                            yield return "end multiple strikes";
                            yield break;
                        }
                        pressList.RemoveAt(ix);
                    }
                    yield break;
                }
                yield return "sendtochaterror Invalid command for Phase 1! Use 'press at ##'.";
            }
            else if (_currentPhase == 1)
            {
                var cmdArr = new string[] { "A1", "B1", "A2", "B2", "TL", "TR", "BL", "BR", "A", "B", "C", "D", "1", "2", "3", "4" };
                var pressList = new List<string>();
                for (int i = 1; i < parameters.Length; i++)
                {
                    var cmd = parameters[i].ToUpperInvariant();
                    if (!cmdArr.Contains(cmd))
                    {
                        yield return "sendtochaterror The given input '" + cmd + "' is invalid!";
                        yield break;
                    }
                    pressList.Add(cmd);
                }
                if (pressList.Count > 16)
                {
                    yield return "sendtochaterror More than 16 presses were given!";
                    yield break;
                }
                yield return "multiple strikes";
                for (int i = 0; i < pressList.Count; i++)
                {
                    GhostMediumSel[Array.IndexOf(cmdArr, pressList[i]) % 4].OnInteract();
                    yield return new WaitForSeconds(0.05f);
                    if (_phaseTwoPressIx == 0)
                    {
                        yield return "end multiple strikes";
                        yield break;
                    }
                }
            }
            else if (_currentPhase == 2)
            {
                var cmdArr = new string[] { "A1", "A2", "A3", "A4", "B1", "B2", "B3", "B4", "C1", "C2", "C3", "C4", "D1", "D2", "D3", "D4", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16" };
                var pressList = new List<string>();
                for (int i = 1; i < parameters.Length; i++)
                {
                    var cmd = parameters[i].ToUpperInvariant();
                    if (!cmdArr.Contains(cmd))
                    {
                        yield return "sendtochaterror The given input '" + cmd + "' is invalid!";
                        yield break;
                    }
                    pressList.Add(cmd);
                }
                if (pressList.Count > 16)
                {
                    yield return "sendtochaterror More than 16 presses were given!";
                    yield break;
                }
                yield return "multiple strikes";
                for (int i = 0; i < pressList.Count; i++)
                {
                    var ix = _phaseThreePressIx;
                    GhostSmallSel[_lazyArr[Array.IndexOf(cmdArr, pressList[i]) % 16]].OnInteract();
                    yield return new WaitForSeconds(0.05f);
                    if (_phaseThreePressIx == ix)
                    {
                        yield return "end multiple strikes";
                        yield break;
                    }
                }
            }
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        AutoSpikeAvoid();
        if (_currentPhase == 0)
        {
            if (_bombTimeSeconds < 60)
            {
                while (_largeHitCount < 4)
                {
                    GhostLargeSel.OnInteract();
                    AutoSpikeAvoid();
                    yield return new WaitForSeconds(0.1f);
                }
                yield return new WaitForSeconds(0.5f);
                goto nextPhase;
            }
            if (!_largePressRules[0])
            {
                while (!(_tensDigit % 2 == 0 && _onesDigit % 2 == 0))
                {
                    yield return null;
                    AutoSpikeAvoid();
                }
                GhostLargeSel.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            if (!_largePressRules[1])
            {
                while (!(_tensDigit % 2 == 1 && _onesDigit % 2 == 1))
                {
                    yield return null;
                    AutoSpikeAvoid();
                }
                GhostLargeSel.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            if (!_largePressRules[2])
            {
                while (_tensDigit != _onesDigit)
                {
                    yield return null;
                    AutoSpikeAvoid();
                }
                GhostLargeSel.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            if (!_largePressRules[3])
            {
                AutoSpikeAvoid();
                GhostLargeSel.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            yield return new WaitForSeconds(0.5f);
        }
        nextPhase:
        if (_currentPhase == 1)
        {
            for (int i = _phaseTwoPressIx; i < _phaseTwoPressOrder.Length; i++)
            {
                GhostMediumSel[_phaseTwoPressOrder[_phaseTwoPressIx]].OnInteract();
                AutoSpikeAvoid();
                yield return new WaitForSeconds(0.1f);
            }
            yield return new WaitForSeconds(0.5f);
        }
        if (_currentPhase == 2)
        {
            for (int i = _phaseThreePressIx; i < _phaseThreePressOrder.Length; i++)
            {
                GhostSmallSel[_lazyArr[_phaseThreePressOrder[_phaseThreePressIx]]].OnInteract();
                AutoSpikeAvoid();
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    private void AutoSpikeAvoid()
    {
        if (_ledPosition == _spikeTarget)
            LedSels[(_ledPosition + 1) % 3].OnInteract();
    }
}