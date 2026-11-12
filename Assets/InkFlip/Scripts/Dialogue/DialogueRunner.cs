using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InkFlip
{
    // Runs conversations with NPCTalker characters in the 3D rooms, using the same NPCData assets, CSV and
    // line / choice format as the original 2D game (NPCData, DialogueLine, DialogueChoice).
    //
    // Add one to the scene, along with a DialogueView (the dialogue UI prefab; Tools > InkFlip > Add Dialogue UI To Scene).
    // The view holds all the visuals, so restyling is done on that prefab. While a conversation is open the player
    // can't move or use tools and the cursor is free; when it ends everything goes back to how it was.
    //   F (gamepad X) or tap the prompt: start talking     Space / Enter / click: continue     1-4: pick a choice
    //   Esc: leave the conversation
    //
    // What a choice can do (the choice_N_action columns in Dialogue.csv):
    //   CompleteRoom  - finishes the room whose id is in the param column        (e.g. glove_room)
    //   GrantAbility  - gives the player a tool in every room, by Ability name   (e.g. InkBlaster)
    //   StartGame     - starts a mini-game by id; the game opens once the conversation has closed (e.g. line_race)
    // plus everything the CSV already does: jump to another line (set key / next row), end the conversation.
    // The 2D game's drawing-quest actions do nothing here.
    public class DialogueRunner : MonoBehaviour
    {
        public FirstPersonController player;
        public ToolSwitcher tools;
        [Tooltip("The dialogue UI. Found automatically if left empty.")]
        public DialogueView view;

        public Key talkKey = Key.F;

        [Header("Text")]
        [Tooltip("Characters revealed per second. 0 shows each line at once.")]
        public float charactersPerSecond = 70f;

        public static DialogueRunner Instance { get; private set; }

        public bool IsActive { get; private set; }
        public NPCData CurrentNpc => npc;

        public static event Action<NPCData> ConversationStarted;
        public static event Action<NPCData> ConversationEnded;

        const int MaxChoices = 4;

        // state
        NPCTalker talker;
        NPCData npc;
        DialogueLine line;
        NPCTalker candidate;
        string fullText = "";
        float revealed;
        bool typing;
        int voiceIndex;
        int startedFrame = -1;
        float noticeTimer;
        AudioSource voice;
        SelectionDrawMode drawMode;
        SculptStudio studio;
        readonly List<GameObject> choiceObjects = new List<GameObject>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            HudFactory.EnsureEventSystem();
        }

        // wait until every Awake has run, so the view exists
        void Start()
        {
            if (view == null) view = FindAnyObjectByType<DialogueView>();
            if (view == null)
            {
                Debug.LogError("DialogueRunner: no DialogueView in the scene. Use Tools > InkFlip > Add Dialogue UI To Scene.", this);
                enabled = false;
                return;
            }

            if (view.promptButton != null) view.promptButton.onClick.AddListener(StartFromPrompt);
            if (view.continueButton != null) view.continueButton.onClick.AddListener(OnContinue);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (view != null)
            {
                if (view.promptButton != null) view.promptButton.onClick.RemoveListener(StartFromPrompt);
                if (view.continueButton != null) view.continueButton.onClick.RemoveListener(OnContinue);
            }
        }

        // ---------- every frame ----------

        void Update()
        {
            if (noticeTimer > 0f)
            {
                noticeTimer -= Time.unscaledDeltaTime;
                if (noticeTimer <= 0f) view.ShowNotice("");
            }

            if (IsActive)
            {
                UpdateConversation();
                return;
            }

            UpdatePrompt();
        }

        void Resolve()
        {
            if (player == null) player = FindAnyObjectByType<FirstPersonController>();
            if (tools == null) tools = FindAnyObjectByType<ToolSwitcher>();
            if (drawMode == null) drawMode = FindAnyObjectByType<SelectionDrawMode>();
            if (studio == null) studio = FindAnyObjectByType<SculptStudio>();
        }

        // find the character you're close to and looking at, show the prompt, start on F
        void UpdatePrompt()
        {
            Resolve();
            Camera look = Camera.main;
            bool busy = look == null || player == null
                        || (drawMode != null && drawMode.IsActive)
                        || (studio != null && studio.IsActive)
                        || LineRoomUI.Busy;

            candidate = busy ? null : FindCandidate(look);
            view.SetPrompt(candidate != null, candidate != null ? $"Press {talkKey} to talk to {candidate.DisplayName}" : "");
            if (candidate == null) return;

            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool pressed = (keyboard != null && keyboard[talkKey].wasPressedThisFrame && FirstPersonController.InputCaptured)
                           || (pad != null && pad.buttonWest.wasPressedThisFrame);
            if (pressed) Begin(candidate);
        }

        NPCTalker FindCandidate(Camera look)
        {
            NPCTalker best = null;
            float bestDistance = float.MaxValue;
            foreach (NPCTalker t in NPCTalker.All)
            {
                if (t == null || t.npc == null) continue;

                Vector3 toward = t.Point - look.transform.position;
                float distance = toward.magnitude;
                if (distance > t.talkRange || distance >= bestDistance) continue;
                if (distance > 0.8f && Vector3.Dot(look.transform.forward, toward / distance) < t.facingNeeded) continue;

                best = t;
                bestDistance = distance;
            }
            return best;
        }

        // the prompt is also a button, for touch (or when the mouse is free)
        void StartFromPrompt()
        {
            if (!IsActive && candidate != null) Begin(candidate);
        }

        // ---------- starting and ending ----------

        void Begin(NPCTalker who)
        {
            string key = NpcMemory.GetKey(who.npc);
            DialogueLine opening = who.npc.GetConversation(key);
            if (opening == null)
            {
                Debug.LogWarning($"DialogueRunner: {who.npc.npcName} has no conversation '{key}' (and no default).", who);
                return;
            }

            talker = who;
            npc = who.npc;
            voiceIndex = 0;
            IsActive = true;
            startedFrame = Time.frameCount;

            // freeze the player and tools, free the cursor
            if (player != null) player.enabled = false;
            if (tools != null) tools.enabled = false;
            FirstPersonController.SetCursorCaptured(false);

            view.SetPrompt(false, "");
            view.SetConversationVisible(true);
            ShowLine(opening);
            ConversationStarted?.Invoke(npc);
        }

        public void End()
        {
            if (!IsActive) return;

            IsActive = false;
            ClearChoices();
            view.SetConversationVisible(false);
            if (voice != null) voice.Stop();

            if (player != null) player.enabled = true;
            if (tools != null) tools.enabled = true;
            FirstPersonController.SetCursorCaptured(true);

            NPCData finished = npc;
            talker = null;
            npc = null;
            line = null;
            ConversationEnded?.Invoke(finished);
        }

        // ---------- lines and choices ----------

        void ShowLine(DialogueLine next)
        {
            line = next;
            if (view.nameText != null) view.nameText.text = npc.npcName;
            PlayVoice();

            fullText = next.npcText ?? "";
            revealed = 0f;
            typing = charactersPerSecond > 0f && fullText.Length > 0;
            if (view.bodyText != null)
            {
                view.bodyText.text = fullText;
                view.bodyText.maxVisibleCharacters = typing ? 0 : int.MaxValue; // reveal letter by letter, keeping any rich text
            }

            RefreshButtons();
        }

        // choices (or the continue button) only appear once the whole line is on screen
        void RefreshButtons()
        {
            ClearChoices();
            if (view.continueButton != null) view.continueButton.gameObject.SetActive(false);
            if (typing) return;

            bool hasChoices = line.choices != null && line.choices.Count > 0;
            if (!hasChoices)
            {
                if (view.continueButton != null) view.continueButton.gameObject.SetActive(true);
                return;
            }
            if (view.choiceButtonPrefab == null || view.choiceContainer == null)
            {
                Debug.LogWarning("DialogueRunner: the DialogueView has no Choice Button Prefab / Choice Container.", view);
                return;
            }

            int count = Mathf.Min(line.choices.Count, MaxChoices);
            for (int i = 0; i < count; i++)
            {
                int index = i;
                Button button = Instantiate(view.choiceButtonPrefab, view.choiceContainer);
                button.gameObject.SetActive(true);
                button.onClick.AddListener(() => OnChoice(index));

                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = (view.numberChoices ? $"{i + 1}.  " : "") + line.choices[i].choiceText;
                choiceObjects.Add(button.gameObject);
            }
        }

        void ClearChoices()
        {
            foreach (GameObject spawned in choiceObjects)
            {
                if (spawned == null) continue;
                spawned.SetActive(false); // gone from the layout right away; destroyed at the end of the frame
                Destroy(spawned);
            }
            choiceObjects.Clear();
        }

        void OnChoice(int index)
        {
            if (!IsActive || typing || line.choices == null || index >= line.choices.Count) return;
            DialogueChoice choice = line.choices[index];

            if (!string.IsNullOrEmpty(choice.setsConversationKey)) NpcMemory.SetKey(npc, choice.setsConversationKey);

            switch (choice.action)
            {
                case ChoiceAction.CompleteRoom:
                    if (!RoomZone.CompleteRoom(choice.actionParam))
                    {
                        Debug.LogWarning($"DialogueRunner: no room with id '{choice.actionParam}' to complete.");
                    }
                    break;

                case ChoiceAction.GrantAbility:
                    GrantAbility(choice.actionParam);
                    break;

                case ChoiceAction.StartGame:
                    MiniGameEvents.Request(choice.actionParam);
                    break;

                case ChoiceAction.OpenDrawingQuest:
                case ChoiceAction.TriggerQuestCheck:
                    Debug.LogWarning($"DialogueRunner: '{choice.action}' is a 2D-game action and does nothing in the 3D rooms.");
                    break;
            }

            if (choice.endsConversation) End();
            else if (choice.nextLine != null && !string.IsNullOrEmpty(choice.nextLine.npcText)) ShowLine(choice.nextLine);
            else End();
        }

        void OnContinue()
        {
            if (!IsActive || typing) return;

            if (!string.IsNullOrEmpty(line.nextLineKey))
            {
                DialogueLine next = npc.GetConversation(line.nextLineKey);
                if (next != null)
                {
                    ShowLine(next);
                    return;
                }
            }
            End();
        }

        void GrantAbility(string abilityName)
        {
            if (!Enum.TryParse(abilityName, true, out Ability ability) || ability == Ability.None)
            {
                Debug.LogWarning($"DialogueRunner: '{abilityName}' isn't an ability (try InkBlaster, PaintBrush, Pencil, Glove or Sculpt).");
                return;
            }

            Abilities.Grant(ability);
            string pretty = Regex.Replace(ability.ToString(), "(?<=[a-z])(?=[A-Z])", " ");
            view.ShowNotice($"New ability: {pretty}!");
            noticeTimer = 3.5f;
        }

        void PlayVoice()
        {
            if (npc.voiceClips == null || npc.voiceClips.Length == 0 || voice == null) return;
            AudioClip clip = npc.voiceClips[voiceIndex % npc.voiceClips.Length];
            voiceIndex = (voiceIndex + 1) % npc.voiceClips.Length;
            if (clip == null) return;
            voice.clip = clip;
            voice.Play();
        }

        // ---------- the conversation each frame ----------

        void UpdateConversation()
        {
            // turn to face the character
            if (talker != null && player != null)
            {
                Vector3 toward = talker.transform.position - player.transform.position;
                toward.y = 0f;
                if (toward.sqrMagnitude > 0.01f)
                {
                    Quaternion face = Quaternion.LookRotation(toward);
                    player.transform.rotation = Quaternion.Slerp(player.transform.rotation, face, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
                }
            }

            // reveal the line a few characters at a time
            if (typing && view.bodyText != null)
            {
                revealed += charactersPerSecond * Time.unscaledDeltaTime;
                view.bodyText.maxVisibleCharacters = Mathf.FloorToInt(revealed);
                if (revealed >= fullText.Length) FinishTyping();
            }

            // the frame a conversation starts, the key that started it shouldn't also advance it
            if (Time.frameCount == startedFrame) return;

            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            if (keyboard == null && pad == null) return;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                FirstPersonController.ConsumeEscape();
                End();
                return;
            }

            bool advance = (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                                                 || keyboard[talkKey].wasPressedThisFrame))
                           || (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame));

            if (advance)
            {
                if (typing) FinishTyping();
                else if (line.choices == null || line.choices.Count == 0) OnContinue();
                return;
            }

            if (typing || keyboard == null || line.choices == null) return;
            Key[] numbers = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
            for (int i = 0; i < numbers.Length && i < line.choices.Count; i++)
            {
                if (keyboard[numbers[i]].wasPressedThisFrame)
                {
                    OnChoice(i);
                    return;
                }
            }
        }

        void FinishTyping()
        {
            typing = false;
            if (view.bodyText != null) view.bodyText.maxVisibleCharacters = int.MaxValue;
            RefreshButtons();
        }
    }
}
