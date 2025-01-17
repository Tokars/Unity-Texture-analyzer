﻿/** ********************************************************************************
* Texture Viewer
* @ 2019 RNGTM
***********************************************************************************/

namespace TextureTool
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using UnityEditor;
    using UnityEditor.IMGUI.Controls;

    /** ********************************************************************************
     * @summary プロジェクト内テクスチャ一覧ツール
     ***********************************************************************************/
    internal class TextureAnalyzerWindow : EditorWindow
    {
        // TreeView レイアウト
        private static readonly GUILayoutOption[] TreeViewLayoutOptions = new GUILayoutOption[]
        {
            GUILayout.ExpandHeight(true)
        };

        [SerializeField] private TextureTreeViewState treeViewState = null; // TreeViewの状態
        [SerializeField] private TextureColumnHeaderState headerState = null; // TreeViewヘッダー状態
        [SerializeField] private SearchState[] columnSearchStates = new SearchState[0]; // 検索状態 (列)
        [System.NonSerialized] private Texture2D[] textures = new Texture2D[0]; // ロードしたテクスチャ
        [System.NonSerialized] private TextureImporter[] textureImporters = new TextureImporter[0];
        private TextureTreeView treeView = null; // TreeView 
        private SearchField searchField = null; // 検索窓

        [SerializeField] MultiColumnHeaderState columnHeaderState;
        private bool isLoadingTexture = false;
        private bool isCreatingTreeView = false;

        /** ********************************************************************************
        * @summary ウィンドウを開く - Open window.
        ***********************************************************************************/
        [MenuItem("Window/Analysis/Texture Analyzer")]
        private static void OpenWindow()
        {
            var window = GetWindow<TextureAnalyzerWindow>();
            window.titleContent = ToolConfig.WindowTitle;

            var position = window.position;
            position.width = ToolConfig.InitialHeaderTotalWidth + 50f;
            position.height = 400f;
            window.position = position;

            window.CreateTreeView(); // 起動直後にTreeView作成
        }

        /** ********************************************************************************
        * @summary ウィンドウの描画 - Draw window.
        ***********************************************************************************/
        private void OnGUI()
        {
            MyStyle.CreateGUIStyleIfNull();
            if (treeView == null)
            {
                CreateTreeView();
            }

            DrawHeader();
            DrawTreeView();
        }

        /** ********************************************************************************
        * @summary TreeViewなどの描画
        ***********************************************************************************/
        private void DrawTreeView()
        {
            var rect = EditorGUILayout.GetControlRect(false, GUILayout.ExpandHeight(true));

            if (isCreatingTreeView)
            {
                EditorGUI.BeginDisabledGroup(true);
                treeView?.OnGUI(rect);
                EditorGUI.EndDisabledGroup();

                rect.position += MyStyle.LoadingLabelPosition;
                EditorGUI.LabelField(rect, ToolConfig.CreatingMessage, MyStyle.LoadingLabel);
            }
            else if (isLoadingTexture)
            {
                EditorGUI.BeginDisabledGroup(true);
                treeView?.OnGUI(rect);
                EditorGUI.EndDisabledGroup();

                rect.position += MyStyle.LoadingLabelPosition;
                EditorGUI.LabelField(rect, ToolConfig.LoadingMessage, MyStyle.LoadingLabel);
            }
            else
            {
                treeView?.OnGUI(rect);
            }
        }

        /** ********************************************************************************
        * @summary ウィンドウ上部のヘッダー描画 - Header drawing at the top of the window.
        ***********************************************************************************/
        private void DrawHeader()
        {
            var defaultColor = GUI.backgroundColor;

            // 検索窓を描画
            using (new EditorGUILayout.HorizontalScope(EditorStyles.label))
            {
                DrawButtons();
                GUI.backgroundColor = defaultColor;

                GUILayout.Space(100);

                GUILayout.FlexibleSpace();
            }

            GUI.backgroundColor = defaultColor;
        }

        /** ********************************************************************************
        * @summary リロードボタン - reload btn.
        ***********************************************************************************/
        private void DrawButtons()
        {
            if (treeView == null) return;
            if (GUILayout.Button("Reload"))
            {
                CreateTreeView();
                ReloadTexture();

                headerState.ResetSearch();

                treeView.SetTexture(textures, textureImporters);
                treeView.Reload(); // Reloadを呼ぶとBuildRootが実行され、次にBuildRowsが実行されます。

                EditorApplication.delayCall += () => treeView.searchString = TextureTreeView.defaultSearchString;
            }
            GUI.backgroundColor = new Color(0.5F, 1F, 0.3F, 1);

            // if (GUILayout.Button(nameof(Generate), GUILayout.MaxWidth(120)))

            if (GUILayout.Button("Recommended texture formats"))
                Application.OpenURL("https://docs.unity3d.com/Manual/class-TextureImporterOverride.html");
        }

        /** ********************************************************************************
        * @summary TreeView の更新 Update
        ***********************************************************************************/
        private void CreateTreeView()
        {
            if (treeView != null)
            {
                return;
            }

            if (isCreatingTreeView)
            {
                return;
            }

            isCreatingTreeView = true;
            Repaint();

            EditorApplication.delayCall += () =>
            {
                if (columnSearchStates == null || columnSearchStates.Length != ToolConfig.HeaderColumnNum)
                {
                    columnSearchStates = new SearchState[ToolConfig.HeaderColumnNum];
                    for (int i = 0; i < ToolConfig.HeaderColumnNum; i++)
                    {
                        columnSearchStates[i] = new SearchState();
                    }
                }


                treeViewState = treeViewState ?? new TextureTreeViewState();
                headerState = headerState ?? new TextureColumnHeaderState(ToolConfig.HeaderColumns, columnSearchStates);
                headerState.ResetSearch();

                // TreeView作成
                treeView = treeView ?? new TextureTreeView(treeViewState, headerState);
                treeView.searchString = TextureTreeView.defaultSearchString;
                treeView.Reload(); // Reloadを呼ぶとBuildRootが実行され、次にBuildRowsが実行されます。

                // SearchFieldを初期化
                searchField = new SearchField();
                searchField.downOrUpArrowKeyPressed += treeView.SetFocusAndEnsureSelectedItem;

                isCreatingTreeView = false;
            };
        }

        /** ********************************************************************************
        * @summary 指定したディレクトリからアセット一覧を取得 - Get the asset list from the specified directory.
        ***********************************************************************************/
        public static IEnumerable<string> GetAssetPaths(string[] directories, string filter = "")
        {
            for (int i = 0; i < directories.Length; i++)
            {
                var directory = directories[i];
                if (directory[directory.Length - 1] == '/')
                {
                    directory = directory.Substring(0, directory.Length - 1);
                }
            }

            var paths = AssetDatabase.FindAssets(filter, directories)
                .Select(x => AssetDatabase.GUIDToAssetPath(x))
                .Where(x => !string.IsNullOrEmpty(x))
                .OrderBy(x => x);

            return paths;
        }


        /** ********************************************************************************
        * @summary ウィンドウにフォーカスが乗ったら呼ばれる - Called when the window is in focus.
        ***********************************************************************************/
        private void OnFocus()
        {
            treeView?.UpdateDataSize();
        }

        /** ********************************************************************************
        * @summary テクスチャのロード - Loading texture
        ***********************************************************************************/
        public void ReloadTexture()
        {
            if (isLoadingTexture) return;

            isLoadingTexture = true;
            CustomUI.DisplayProgressLoadTexture();

            // 指定ディレクトリからテクスチャロード - Load texture from specified directory.
            var paths = GetAssetPaths(ToolConfig.TargetDirectories, "t:texture2d");
            var textureList = new List<Texture2D>();
            var importerList = new List<TextureImporter>();
            foreach (var path in paths)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                textureList.Add(texture);
                importerList.Add(importer);
            }

            textures = textureList.ToArray();
            textureImporters = importerList.ToArray();

            EditorUtility.ClearProgressBar();

            isLoadingTexture = false;
        }
    }
}