# nnl

VRChat avatars向けのNeural Network Shaderを研究・実装するUnityプロジェクトです。

## 開発環境

| 項目 | 設定 |
| --- | --- |
| Unity | 2022.3.22f1 |
| Render Pipeline | Built-in Render Pipeline |
| VRChat SDK - Base | 3.10.6 |
| VRChat SDK - Avatars | 3.10.6 |
| VRChat Package Resolver | 0.1.29 |
| Gesture Manager | 3.9.9 |

Unityのバージョンは `ProjectSettings/ProjectVersion.txt`、VRChat関連パッケージは `Packages/vpm-manifest.json` を参照してください。

## 目的

Shader内でニューラルネットワークの推論を実行し、アバター表現への応用を検証します。

検討しているテーマ:

- 小規模MLPによるToon Lighting
- キャラクター表面の間接光・自己遮蔽表現
- Pythonで学習した重みのUnity Shaderへの取り込み
- 推論頻度を抑えた場合の時間方向の補間・結果再利用
- VRChat上での実行負荷と描画品質の評価

## 方針

- まずはPC向けVRChatアバターを対象にする
- Shader内で実行可能な構成を優先する
- 学習処理と推論処理を分離し、学習済み重みをUnityへ取り込める形にする
- 実機・VRChat上で負荷を計測しながら段階的に拡張する

## 開発状況

プロジェクトの初期セットアップ段階です。実験内容・実装状況は進行に合わせて更新します。
