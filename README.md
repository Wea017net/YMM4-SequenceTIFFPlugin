# 連番TIFF画像プラグイン

「図形」アイテムに **連番TIFF画像** を追加し、連番TIFF画像およびマルチページTIFF画像を扱えるようにするプラグインです。

## 使い方

1. 「図形」アイテムを追加します。
2. 図形の種類から「連番TIFF画像」を選びます。
3. 「1枚目のファイル」で連番の先頭画像を選びます。
4. マルチページTIFFの場合は「ページ」を1始まりで指定します（初期値1）。
5. 必要に応じて「再生開始位置」を1始まりで指定します（初期値1）。
6. 「フレームレート」を1～240 fpsの整数で指定します（初期値30 fps）。
7. 繰り返す場合は「ループ」を有効にします。「ループ終了フレーム」が0の場合は連番の末尾、1以上の場合は指定フレームまでを繰り返します。

`render_0001.tif`, `render_0002.tif`, `render_0003.tif` のように、拡張子直前の数字を連番として認識します。`.tif`と`.tiff`の両方に対応し、選択した番号以降の画像を数値順で再生します。ファイル名に末尾の数字がない場合は、選択画像だけを表示します。

ファイル未指定の状態から1枚目を選択すると、その時点のフレームレートと画像枚数からアイテム長を自動設定します。この長さはプロジェクトの1秒分より短くなりません。設定済みのファイルを別のファイルへ変更した場合、長さは変更しません。

アイテムを分割すると、後半アイテムの「再生開始位置」が分割位置に合わせて進み、映像が連続します。タイムライン上には連番素材の終了地点を示す点線が表示されます。ループが無効でアイテムの長さが連番より長い場合は、最後の画像を保持します。

## インストール

配布物の `SequenceTIFFPlugin.ymme` をダブルクリックし、YMM4の案内に従ってインストールしてください。

手動の場合は、`SequenceTIFFPlugin.dll` と8言語分のカルチャーフォルダーを次のように配置してYMM4を再起動します。

```text
<YMM4フォルダー>\user\plugin\SequenceTIFFPlugin\
├─ SequenceTIFFPlugin.dll
├─ ar-SA\SequenceTIFFPlugin.resources.dll
├─ en-US\SequenceTIFFPlugin.resources.dll
├─ es-ES\SequenceTIFFPlugin.resources.dll
├─ id-ID\SequenceTIFFPlugin.resources.dll
├─ ja-JP\SequenceTIFFPlugin.resources.dll
├─ ko-KR\SequenceTIFFPlugin.resources.dll
├─ zh-CN\SequenceTIFFPlugin.resources.dll
└─ zh-TW\SequenceTIFFPlugin.resources.dll
```

## 動作要件

- OS: Windows 10 / 11 64-bit
- ゆっくりMovieMaker4 （最新版を推奨）
- ランタイム: .NET 10.0

## 対応言語

- 日本語 (ja-jp)
- 英語 (en-us)
- 簡体字中国語 (zh-cn)
- 繁体字中国語 (zh-tw)
- 韓国語 (ko-kr)
- アラビア語 (ar-sa)
- インドネシア語 (id-id)
- スペイン語 (es-es)

## ビルド

必要なもの:

- .NET 10 SDK
- YukkuriMovieMaker4 v4.56.1.0（動作確認に使用。最新版推奨）

`Directory.Build.props.example` を `Directory.Build.props` という名前でコピーし、`YMM4DirPath`を設定してからビルドします。

```powershell
Copy-Item Directory.Build.props.example Directory.Build.props
dotnet build -c Release
```

またはプロパティをコマンドラインで直接指定できます。

```powershell
dotnet build -c Release -p:YMM4DirPath="C:\Path\To\YukkuriMovieMaker4\"
```

配布用の`.ymme`は次のコマンドで`dist`フォルダーへ作成できます。

```powershell
.\scripts\package.ps1 -Ymm4DirPath "C:\Path\To\YukkuriMovieMaker4\"
```

## 注意事項
> [!IMPORTANT]
> - Exo出力には対応していません。YMM4からの動画出力でのみ使用できます。
> - TIFF は Windows Imaging Component (WIC) でデコードし、描画時に 32bit premultiplied BGRA へ変換します。
> - 作者は､本プラグインの利用に起因するいかなる損害についても､一切の責任を負いません｡
> - プラグインの制作には Codex を使用しています。人間によるテストを十分に行っていますが、不安な方は各自でソースコードの確認をお願いします。

## ライセンス

MIT License
