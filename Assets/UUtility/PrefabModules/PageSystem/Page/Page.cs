using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

using UTool.Utility;
using UTool.Tweening;

namespace UTool.PageSystem
{
    public class Page : MonoBehaviour
    {
        [SerializeField] private TweenElement pageOpenCloseTE;
        [SerializeField] private TweenElement contentOpenCloseTE;
        [SpaceArea]
        [SerializeField][BeginGroup] private Page parentPage;
        [SerializeField][Disable] private Page activeSubPage;
        [SerializeField][Disable] public bool isOpen = false;
        [SerializeField][Disable] public bool isActive = false;
        [SpaceArea]
        [SerializeField] private bool masterPage = false;
        [SerializeField] private int currentPageIndex = 0;
        [SerializeField] private bool autoDisablePage = false;
        [SerializeField] private bool keepContentOverlayOpen = false;
        [SpaceArea]
        [Title("OnAwake")]
        [SerializeField] private bool openSubPageInstant = true;
        [SerializeField][EndGroup] private bool closeSubPageInstant = true;
        [SpaceArea]
        [SerializeField][BeginGroup][SearchableEnum] private FadeType contentFadeType = FadeType.FadeOutIn;
        [SpaceArea]
        [SerializeField][SearchableEnum] private FadeType nextPageFadeType = FadeType.FadeOutIn;
        [SerializeField][SearchableEnum] private FadeType previousPageFadeType = FadeType.FadeOutIn;
        [SpaceArea]
        [SerializeField][SearchableEnum] private FadeType goToUpperPageFadeType = FadeType.FadeOutIn;
        [SerializeField][EndGroup][SearchableEnum] private FadeType goToLowerPageFadeType = FadeType.FadeOutIn;
        [SpaceArea]
        [EditorButton(nameof(OpenPage), activityType: ButtonActivityType.OnPlayMode)]
        [EditorButton(nameof(ClosePage), activityType: ButtonActivityType.OnPlayMode)]
        [EditorButton(nameof(NextPage), activityType: ButtonActivityType.OnPlayMode)]
        [EditorButton(nameof(PreviousPage), activityType: ButtonActivityType.OnPlayMode)]
        [SpaceArea, Line(5), SpaceArea]

        [SpaceArea]
        [EditorButton(nameof(GetSubPages))]
        [SerializeField] private Transform subPageHolder;
        [SerializeField][ReorderableList(Foldable = true)] private List<Page> subPages = new List<Page>();

        [SpaceArea, Line(5), SpaceArea]

        [SerializeField][BeginGroup("Events")] private UnityEvent OnOpening = new UnityEvent();
        [SerializeField] private UnityEvent OnOpened = new UnityEvent();
        [SpaceArea]
        [SerializeField] private UnityEvent OnClosing = new UnityEvent();
        [SerializeField] private UnityEvent OnClosed = new UnityEvent();
        [SpaceArea, Line(5), SpaceArea]
        [SerializeField] private UnityEvent OnPageActive = new UnityEvent();
        [SerializeField] private UnityEvent OnPageDeactive = new UnityEvent();
        [SerializeField] private UnityEvent<bool> OnAnyPageActive = new UnityEvent<bool>();
        [SpaceArea, Line(5), SpaceArea]
        [SerializeField] private UnityEvent<bool> OnRequest = new UnityEvent<bool>();
        [SerializeField] private UnityEvent<bool> OnStateCompleted = new UnityEvent<bool>();
        [SerializeField] private UnityEvent<bool, bool> OnAnyStateChanged = new UnityEvent<bool, bool>();
        [SpaceArea, Line(5), SpaceArea]
        [SerializeField] private UnityEvent<int> OnPageIndex = new UnityEvent<int>();
        [SpaceArea, Line(5), SpaceArea]
        [SerializeField] private UnityEvent OnStartPageReached = new UnityEvent();
        [SerializeField][EndGroup] private UnityEvent OnLastPageReached = new UnityEvent();

        public int subPageCount => subPages.Count;

        private ActionState previousFadeAction;
        private ActionState previousContentFadeAction;

        #region Internal

        public void OpenPage() => Open();
        public void ClosePage() => Close();
        public void NextPage() => Next();
        public void PreviousPage() => Previous();

        public void OpenParentPage() => Open(requestParent: true);
        public void CloseParentPage() => Close(requestParent: true);
        public void NextParentPage() => Next(requestParent: true);
        public void PreviousParentPage() => Previous(requestParent: true);
        public void CloseParentSubPage() => CloseParentActiveSubPage();

        public void _OnRequest(bool state)
        {
            isOpen = state;
            isActive = state;

            if (isActive)
                OnPageActive?.Invoke();
            else
                OnPageDeactive?.Invoke();

            OnAnyPageActive?.Invoke(isActive);

            //if (state)
            //    isOpen = true;

            if (state)
                OnOpening?.Invoke();
            else
                OnClosing.Invoke();

            OnRequest?.Invoke(state);
            OnAnyStateChanged?.Invoke(false, state);
        }

        public void _OnComplete(bool state)
        {
            //if (!state)
            //    isOpen = false;

            if (state)
                OnOpened?.Invoke();
            else
                OnClosed.Invoke();

            OnStateCompleted?.Invoke(state);
            OnAnyStateChanged?.Invoke(true, state);
        }

        private void GetSubPages()
        {
            subPages.Clear();

            for (int i = 0; i < subPageHolder.childCount; i++)
                if (subPageHolder.GetChild(i).TryGetComponent(out Page page))
                {
                    page.parentPage = this;
                    page.RecordPrefabChanges();
                    subPages.Add(page);
                }

            gameObject.ForceRecordPrefabChanges();
        }

        #endregion

        private void Awake()
        {
            //if (!openSubpageOnAwake)
            //    currentPageIndex = -1;

            for (int i = 0; i < subPages.Count; i++)
            {
                if (i == currentPageIndex)
                {
                    activeSubPage = subPages[i];
                    activeSubPage.Open(instant: openSubPageInstant);
                    continue;
                }

                subPages[i].Close(instant: closeSubPageInstant);
            }
        }

        public void Open(bool requestParent = false, bool instant = false, Action onComplete = null)
        {
            if (requestParent)
            {
                if (parentPage)
                    parentPage.Open(instant: instant, onComplete: onComplete);

                return;
            }

            if (autoDisablePage)
                gameObject.SetActive(true);

            pageOpenCloseTE.PlayTween(instant: instant, onComplete);
        }

        public void Close(bool requestParent = false, bool instant = false, Action onComplete = null)
        {
            if (requestParent)
            {
                if (parentPage)
                    parentPage.Close(instant: instant, onComplete: onComplete);

                return;
            }

            CloseActiveSubPage(instant);
            pageOpenCloseTE.ReverseTween(instant, 
                () =>
                {
                    if (autoDisablePage)
                        gameObject.SetActive(false);
                    onComplete?.Invoke();
                });
        }

        public void CloseParentActiveSubPage(bool instant = false, Action onComplete = null)
        {
            if(parentPage)
                parentPage.CloseActiveSubPage(instant: instant, onComplete: onComplete);
        }

        public void CloseActiveSubPage(bool instant = false, Action onComplete = null)
        {
            if (!activeSubPage)
            {
                onComplete?.Invoke();
                return;
            }

            activeSubPage.CloseActiveSubPage(instant);

            if (isOpen)
            {
                isActive = true;

                if (isActive)
                    OnPageActive?.Invoke();
                else
                    OnPageDeactive?.Invoke();

                OnAnyPageActive?.Invoke(isActive);
            }

            activeSubPage.Close(instant, onComplete: onComplete);
            //activeSubPage.CloseAllSubPages(instant);
            activeSubPage = null;
            currentPageIndex = -1;

            OnPageIndex?.Invoke(-1);
        }

        public void CloseAllSubPages(bool instant = false)
        {
            foreach (Page page in subPages)
            {
                page.Close(instant);
                page.CloseAllSubPages(instant);
            }

            currentPageIndex = -1;
        }

        public void CloseActiveMasterSubPage()
        {
            if (masterPage)
            {
                CloseActiveSubPage();
                OpenPage();

                return;
            }

            if (parentPage)
                parentPage.CloseActiveMasterSubPage();
        }

        public void GoTo(int index) => GoTo(index, null);

        public void GoTo(Page page, Action onComplete = null)
        {
            if (subPages.Contains(page))
                GoTo(subPages.IndexOf(page), onComplete);
            else
                Debug.LogWarning($"[Page] Cant GoTo SubPage : {page.name} | Not part of this Page", gameObject);
        }

        public void GoTo(int pageIndex, Action onComplete = null)
        {
            if (currentPageIndex == pageIndex)
                return;

            if (pageIndex >= 0 && pageIndex < subPages.Count)
            {
                isActive = false;

                if (isActive)
                    OnPageActive?.Invoke();
                else
                    OnPageDeactive?.Invoke();

                OnAnyPageActive?.Invoke(isActive);

                if (currentPageIndex == -1)
                    FadeContent(false, contentFadeType, FadePageLogic);
                else
                    FadePageLogic();

                void FadePageLogic()
                {
                    if (pageIndex > currentPageIndex)
                        FadePage(pageIndex, goToUpperPageFadeType, onComplete);
                    else
                        FadePage(pageIndex, goToLowerPageFadeType, onComplete);
                }
            }
            else
                Debug.LogWarning($"[Page] Cant GoTo SubPage Index : {pageIndex} | Does not Exist", gameObject);
        }

        public void Next(bool requestParent = false, Action onComplete = null)
        {
            if (requestParent)
            {
                if (parentPage)
                    parentPage.Next(onComplete: onComplete);

                return;
            }

            if (currentPageIndex == -1)
                FadeContent(false, contentFadeType, NextSubPage);
            else
                NextSubPage();

            void NextSubPage()
            {
                int nextIndex = currentPageIndex + 1;

                if (nextIndex < subPages.Count)
                    FadePage(nextIndex, nextPageFadeType, onComplete);
                else
                    OnStartPageReached?.Invoke();
            }
        }

        public void Previous(bool requestParent = false, Action onComplete = null)
        {
            if (requestParent)
            {
                if (parentPage)
                    parentPage.Previous(onComplete: onComplete);

                return;
            }

            int previousIndex = currentPageIndex - 1;

            if (subPages.Count != 0 && previousIndex < subPages.Count && previousIndex >= 0)
                FadePage(previousIndex, previousPageFadeType, onComplete);
            else
                OnLastPageReached?.Invoke();
        }

        private void FadeContent(bool state, FadeType fadeType, Action onComplete = null)
        {
            if (!contentOpenCloseTE || keepContentOverlayOpen)
            {
                onComplete?.Invoke();
                return;
            }

            if (previousContentFadeAction != null)
                previousContentFadeAction.Kill();

            ActionState fadeAction = new ActionState();
            previousContentFadeAction = fadeAction;

            switch (fadeType)
            {
                case FadeType.FadeOutIn:
                {
                    contentOpenCloseTE.PlayTween(FadeComplete);
                }
                break;

                case FadeType.CrossFade:
                {
                    if (state)
                        contentOpenCloseTE.ReverseTween();
                    else
                        contentOpenCloseTE.PlayTween();

                    FadeComplete();
                }
                break;

                case FadeType.FadeInOut:
                {
                    contentOpenCloseTE.ReverseTween(FadeComplete);
                }
                break;
            }

            void FadeComplete()
            {
                if (fadeAction.isKilled)
                    return;

                onComplete?.Invoke();
            }
        }

        private void FadePage(int nextIndex, FadeType fadeType, Action onComplete = null)
        {
            Page currentPage = activeSubPage;
            Page nextPage = subPages[nextIndex];

            currentPageIndex = nextIndex;
            activeSubPage = nextPage;

            if (previousFadeAction != null)
                previousFadeAction.Kill();

            ActionState fadeAction = new ActionState();
            previousFadeAction = fadeAction;

            if (currentPage)
            {
                switch (fadeType)
                {
                    case FadeType.FadeOutIn:
                    {
                        currentPage.Close(onComplete: () =>
                        {
                            if (fadeAction.isKilled)
                                return;

                            nextPage.Open(onComplete: FadeComplete);
                        });
                    }
                    break;

                    case FadeType.CrossFade:
                    {
                        int completedCount = 0;

                        currentPage.Close(onComplete: () =>
                        {
                            CheckIfCompleted();
                        });
                        nextPage.Open(onComplete: () =>
                        {
                            CheckIfCompleted();
                        });

                        void CheckIfCompleted()
                        {
                            if (fadeAction.isKilled)
                                return;

                            completedCount++;

                            if (completedCount >= 2)
                                FadeComplete();
                        }
                    }
                    break;

                    case FadeType.FadeInOut:
                    {
                        nextPage.Open(onComplete: () =>
                        {
                            if (fadeAction.isKilled)
                                return;

                            currentPage.Close(onComplete: FadeComplete);
                        });
                    }
                    break;
                }
            }
            else
            {
                nextPage.Open(onComplete: FadeComplete);
            }

            void FadeComplete()
            {
                if (fadeAction.isKilled)
                    return;

                onComplete?.Invoke();
            }

            OnPageIndex?.Invoke(nextIndex);
        }

        private enum FadeType
        {
            FadeOutIn,
            CrossFade,
            FadeInOut
        }

        private class ActionState
        {
            public bool isKilled = false;

            public void Kill()
            {
                isKilled = true;
            }
        }
    }
}