using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using UTool;
using UTool.Utility;

using Coffee.UIEffects;
using DG.Tweening;

namespace UTool.Leaderboard
{
    public class Entrie : MonoBehaviour
    {
        [SerializeField] private CanvasGroup cg;
        [SerializeField] private Image img;
        [SpaceArea]
        [SerializeField] private TextMeshProUGUI rankField;
        [SerializeField] private TextMeshProUGUI nameField;
        [SerializeField] private TextMeshProUGUI scoreField;
        [SpaceArea]
        [SerializeField] private bool setRank = true;
        [SerializeField] private int rankOffset = 0;
        [SpaceArea]
        [SerializeField] private Color topRankColor;
        [SerializeField] private Color defaultColor;
        [SpaceArea]
        [SerializeField][IgnoreParent] public EntrieData entrieData;

        Tween tween;

        private void OnDrawGizmosSelected()
        {
            if (setRank)
            {
                string rank = "#" + (transform.GetSiblingIndex() + rankOffset);

                if (rankField.text != rank)
                {
                    rankField.text = rank;
                    rankField.RecordPrefabChanges();
                }
            }
        }

        public void UpdateInfo(EntrieData _entrieData, bool first, int index)
        {
            entrieData = _entrieData;

            img.color = first ? topRankColor : defaultColor;

            tween.KillTween();
            tween = DOVirtual.Float(0, 1, 0.55f,
                (value) =>
                {
                    cg.alpha = value;
                })
                .SetDelay(index / 7.5f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (entrieData != null)
                    {
                        img.color = first ? topRankColor : defaultColor;

                        TextInfo userNameInfo = new CultureInfo("en-US", false).TextInfo;

                        nameField.text = userNameInfo.ToTitleCase(entrieData.playerName);
                        scoreField.text = "<mspace=0.75e>" + entrieData.score.ToString();
                    }

                    tween = DOVirtual.Float(1, 0, 0.55f,
                        (value) =>
                        {
                            cg.alpha = value;
                        })
                        .SetEase(Ease.OutQuad);
                });
        }

        public void Reset()
        {
            nameField.text = "-";
            scoreField.text = "-";

            entrieData = null;
        }
    }
}