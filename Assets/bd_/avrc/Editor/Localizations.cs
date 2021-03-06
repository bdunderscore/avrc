/*
 * MIT License
 * 
 * Copyright (c) 2021-2022 bd_
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE. 
 */

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace net.fushizen.avrc
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class Localizations
    {
        private static Localizations EN = new Localizations()
        {
            PROP_TYPE_NAMES = new GUIContent[]
            {
                new GUIContent("Bool"),
                new GUIContent("Int"),
            },

            PROP_ROLE_NAMES = new[]
            {
                new GUIContent(""),
                new GUIContent("TX"),
                new GUIContent("RX")
            }
        };

        private static Localizations JA = new Localizations()
        {
            PROP_ROLE_NAMES = new[]
            {
                new GUIContent(""),
                new GUIContent("TX (送信)"),
                new GUIContent("RX (受信)")
            },

            PROP_NOSIG_NAMES = new[]
            {
                new GUIContent("ホールド", "切断された場合、最後の状態を維持する"),
                new GUIContent("リセット", "切断された場合、指定した値にリセットする"),
                new GUIContent("フォワード", "切断された場合、指定したパラメーターをコピーする")
            },

            // Parameters generator
            LANG_SWITCHER = new GUIContent("言語: 日本語"),
            GP_FOLDOUT = new GUIContent("メニューから生成"),
            GP_REF_AVATAR = new GUIContent("参照するアバター", "AVRC はこのアバターのパラメータを使用して新しいパラメータを構成します"),
            GP_ERR_NO_PARAMS = "アバターパラメータアセットが設定されていません",
            GP_ERR_NO_NEW_PARAMS = "新しいパラメータがありません",
            GP_FOUND_PARAMS = "パラメータを検出しました: {0}",
            GP_ADD_PARAMS = new GUIContent("パラメータを追加"),
            GP_ERR_DUPLICATE = "パラメータ名が重複しています: {0}",
            GP_NOTDEF = "パラメータが定義されていません: {0}",
            GP_ERR_PUPPET_TYPE = "ペットパラメータはFloat型ではありません: {0}",
            GP_ERR_PRIMARY_TYPE = "プライマリパラメータはInt型かBool型である必要があります: {0}",

            // AVRC Parameters inspector
            AP_INSTALL = new GUIContent("インストール", "インストールウィンドウを開きます"),
            AP_SRC_MENU = new GUIContent("内包メニュー", "AVRCアセットに内包するメニューアセット"),
            AP_PREFIX = new GUIContent("プレフィックス", "生成されるレイヤーとオブジェクトの名前に付けます"),
            AP_PARAMETERS = new GUIContent("パラメータ"),
            AP_RX_PARAM = new GUIContent("受信パラメータ"),
            AP_RANGE = new GUIContent("範囲"),
            AP_CAN_EMBED = new GUIContent(
                // use spaces to hint where to put linebreaks
                // zero-width spaces don't seem to work:
                // https://issuetracker.unity3d.com/issues/zero-width-space-does-not-cause-line-break-when-wrapping-words
                "Expressions Menuアセット（とそのサブメニュー）をこの Link Spec " +
                "アセットファイルの中に内包することができます。 " +
                "そうすると、 フレンドにそのメニューを渡すときはこの Link Spec " +
                "ファイル一つだけで済みます。"),
            AP_HAS_EMBED = new GUIContent("このLink Specアセットに内包メニューがあります。"),
            AP_CLEAR_EMBED_BUTTON = new GUIContent("内包メニューを消去"),
            AP_CLEAR_EMBED_CANCEL = new GUIContent("内包メニュー消去をキャンセル"),
            AP_CLEAR_EMBED_WARN = new GUIContent("注意：内包メニューを消去したら、戻すことができません。"),
            AP_CLEAR_EMBED_CONFIRM = new GUIContent("内包メニューを消去"),

            // Installer
            INST_TITLE = new GUIContent("AVRC Installer"),
            INST_PARAMS = new GUIContent("リンクスペックアセット"),
            INST_AVATAR = new GUIContent("対象アバター", "インストールするアバター"),
            INST_MENU = new GUIContent("メニュー", "内包メニューをこのメニューに追加します"),
            INST_ROLE = new GUIContent("役割"),
            INST_ADV_SETTINGS = new GUIContent("詳細設定"),
            INST_TIMEOUT = new GUIContent("タイムアウト（秒）", "通信鳴くこの時間が立った場合、相手がいなくなったと見なされます"),
            INST_LAYER_NAME = new GUIContent("レイヤー名", "自動生成レイヤーの名前に追加されます"),
            INST_INSTALL = new GUIContent("インストール"),
            INST_TX = new GUIContent("送信機でインストール", "AVRCパラメータを送信機に設定します"),
            INST_RX = new GUIContent("受信機でインストール", "AVRCパラメータを受信機に設定します"),
            INST_UNINSTALL = new GUIContent("アンインストール", "このAVRCパラメータをアンインストールします"),
            INST_UNINSTALL_ALL = new GUIContent("全てのAVRCパラメータをアンインストール", "全てのAVRCパラメータをアンインストールします"),
            INST_SECRET_MODE = new GUIContent("シークレットモード", "送信者と受信者以外はこの信号が見れません"),
            INST_PARAM_SETTINGS = new GUIContent("パラメーター設定"),
            INST_ERR_NO_ROLE = "役割を設定してください",
            INST_ERR_NO_PARAMS = "AVRCパラメータアセットが設定されていません",
            INST_ERR_NO_PREFIX = "プレフィックスが設定されていません",
            INST_ERR_NO_AVATAR = "対象アバターを選択してください",
            INST_ERR_NO_FX = "対象アバターにはFXレイヤーが必要です",
            INST_ERR_DUP_PARAM = "パラメーター名が重複しています：【{0}】",
            INST_ERR_BAD_TIMEOUT = "タイムアウト値が不正です：{0}",
            INST_ERR_MIXED_WRITE_DEFAULTS = "アバターの既存のWrite Defaults設定が混ざってます。問題になる場合があります。",
            INST_ERR_WD_MISMATCH = "Write Defaultsの設定がアバターの既存のアニメーターと違います。問題になる場合があります。",
            INST_ERR_NO_EXP_PARAMS = "Expression parametersがアバターで設定されていません。",
            INST_ERR_SYNCED_SECRET_PARAM = "シークレットパラメーター「{0}」の同期設定をExpression Parametersから外してください。",
            INST_MENU_FULL = "選択されたメニューは満杯です",
            TEST_NO_AV3EMU = "このコンポーネントにはLyuma's Avatar 3.0 Emulatorが必要です",
            TEST_OLD_AV3EMU = "Lyuma's Avatar 3.0 emulatorが古すぎます！（バージョン2.9.8以上が必要です）"
        };

        private static Localizations Current = null;

        private GUIContent[] PROP_NOSIG_NAMES_ = Enum.GetNames(typeof(NoSignalMode))
            .Select(n => new GUIContent(n)).ToArray();

        private GUIContent[] PROP_ROLE_NAMES_ = Enum.GetNames(typeof(Role))
            .Select(n => new GUIContent(n)).ToArray();

        private GUIContent[] PROP_TYPE_NAMES_ = Enum.GetNames(typeof(AvrcSignalType))
            .Select(n => new GUIContent(n)).ToArray();

        private Localizations()
        {
        }

        public GUIContent LANG_SWITCHER { get; private set; }
            = new GUIContent("Language: English");

        public GUIContent[] PROP_TYPE_NAMES
        {
            get => PROP_TYPE_NAMES_.Clone() as GUIContent[];
            private set => PROP_TYPE_NAMES_ = value;
        }

        public GUIContent[] PROP_ROLE_NAMES
        {
            get => PROP_ROLE_NAMES_.Clone() as GUIContent[];
            private set => PROP_ROLE_NAMES_ = value;
        }

        public GUIContent[] PROP_NOSIG_NAMES
        {
            get => PROP_NOSIG_NAMES_.Clone() as GUIContent[];
            private set => PROP_NOSIG_NAMES_ = value;
        }

        public static Localizations Inst
        {
            get
            {
                if (Current == null)
                {
                    switch (AvrcPrefs.Get().Language)
                    {
                        case Language.JA:
                            Current = JA;
                            break;
                        default:
                            Current = EN;
                            break;
                    }
                }

                return Current;
            }
        }

        public string TEST_NO_AV3EMU { get; private set; } = "This component requires Lyuma's Avatar 3.0 Emulator.";

        public string TEST_OLD_AV3EMU { get; private set; } =
            "Lyuma's Avatar 3.0 emulator is too old! (must be version 2.9.8 or higher)";

        internal static void SetLanguage(Language lang)
        {
            var prefs = AvrcPrefs.Get();
            prefs.Language = lang;
            Current = null;

            EditorUtility.SetDirty(prefs);
        }

        internal static void SwitchLanguageButton()
        {
            if (GUILayout.Button(Inst.LANG_SWITCHER))
            {
                SetLanguage(AvrcPrefs.Get().Language == Language.EN ? Language.JA : Language.EN);
            }
        }

        #region Parameters generator

        public GUIContent GP_FOLDOUT { get; private set; } = new GUIContent("Generate from expressions menu");

        public GUIContent GP_REF_AVATAR { get; private set; } = new GUIContent("Reference avatar",
            "AVRC will use the expressions parameters from this avatar to configure the new parameters");

        public string GP_ERR_NO_PARAMS { get; private set; } = "No expression parameters found";

        public string GP_ERR_NO_NEW_PARAMS { get; private set; } = "No new parameters found";

        public string GP_FOUND_PARAMS { get; private set; } = "Found parameters: {0}";

        public GUIContent GP_ADD_PARAMS { get; private set; } = new GUIContent("Add parameters");

        public string GP_ERR_DUPLICATE { get; private set; } = "Duplicate parameter name: {0}";

        public string GP_NOTDEF { get; private set; } = "Parameter not defined in expressions parameters: {0}";

        public string GP_ERR_PUPPET_TYPE { get; private set; } = "Puppet menu subparameter is not a float: {0}";

        public string GP_ERR_PRIMARY_TYPE { get; private set; } = "Primary parameter is not an int or bool: {0}";

        #endregion

        #region AVRC Parameters inspector

        public GUIContent AP_INSTALL { get; private set; } = new GUIContent("Install",
            "Open the installation window");

        public GUIContent AP_SRC_MENU { get; private set; } = new GUIContent("Embed expressions menu",
            "Expressions menu to embed in the AVRC parameters"
        );

        public GUIContent AP_PREFIX { get; private set; } = new GUIContent("Prefix",
            "Prefix to add to the generated layers and objects");

        public GUIContent AP_PARAMETERS { get; private set; } = new GUIContent("Parameters");
        public GUIContent AP_RX_PARAM { get; private set; } = new GUIContent("RX parameter");
        public GUIContent AP_RANGE { get; private set; } = new GUIContent("Range");


        public GUIContent AP_CAN_EMBED { get; private set; } = new GUIContent(
            "You can embed an Expressions Menu (and its submenus) into this Link Spec Asset file. " +
            "This will allow you to provide this menu to your friend just by copying this one Link Spec Asset."
        );

        public GUIContent AP_HAS_EMBED { get; private set; } = new GUIContent(
            "This Link Spec Asset contains an embedded menu."
        );

        public GUIContent AP_CLEAR_EMBED_BUTTON { get; private set; } = new GUIContent("Clear embedded menu");
        public GUIContent AP_CLEAR_EMBED_CANCEL { get; private set; } = new GUIContent("Cancel clearing menu");

        public GUIContent AP_CLEAR_EMBED_WARN { get; private set; } =
            new GUIContent("Warning: Once the embedded menu is cleared, it cannot be restored.");

        public GUIContent AP_CLEAR_EMBED_CONFIRM { get; private set; } =
            new GUIContent("Clear embedded menu");

        #endregion

        #region Installer

        public GUIContent INST_TITLE { get; private set; } = new GUIContent("AVRC Installer");
        public GUIContent INST_PARAMS { get; private set; } = new GUIContent("Link Spec");
        public GUIContent INST_AVATAR { get; private set; } = new GUIContent("Target Avatar");

        public GUIContent INST_MENU { get; private set; } = new GUIContent(
            "Install menu under",
            "Installs the embedded submenu underneath this menu"
        );

        public GUIContent INST_ROLE { get; private set; } = new GUIContent("Role");

        public GUIContent INST_WRITE_DEFAULTS { get; } = new GUIContent("Write Defaults");
        public GUIContent INST_ADV_SETTINGS { get; private set; } = new GUIContent("Advanced settings");

        public GUIContent INST_TIMEOUT { get; private set; }
            = new GUIContent("Timeout (seconds)", "Time without communication before we assume the peer is gone");

        public GUIContent INST_LAYER_NAME { get; private set; }
            = new GUIContent("Layer name", "Name to add to generated layers");

        public GUIContent INST_INSTALL { get; private set; } = new GUIContent("Install");
        public GUIContent INST_TX { get; private set; } = new GUIContent("Install as transmitter");
        public GUIContent INST_RX { get; private set; } = new GUIContent("Install as receiver");
        public GUIContent INST_UNINSTALL { get; private set; } = new GUIContent("Uninstall");
        public GUIContent INST_UNINSTALL_ALL { get; private set; } = new GUIContent("Uninstall ALL AVRC components");

        public GUIContent INST_SECRET_MODE { get; private set; }
            = new GUIContent("Secret mode", "Only the transmitter and receiver will see this signal");

        public GUIContent INST_PARAM_SETTINGS { get; private set; } = new GUIContent("Parameter settings");

        public string INST_ERR_NO_ROLE { get; private set; } = "Role is not set";
        public string INST_ERR_NO_PARAMS { get; private set; } = "AVRC Parameters must be set";
        public string INST_ERR_NO_PREFIX { get; private set; } = "Prefix must be set";
        public string INST_ERR_NO_AVATAR { get; private set; } = "Target Avatar must be selected";
        public string INST_ERR_NO_FX { get; private set; } = "Avatar must have an FX layer";
        public string INST_ERR_DUP_PARAM { get; private set; } = "Duplicate parameter name [{0}]";
        public string INST_ERR_BAD_TIMEOUT { get; private set; } = "Invalid timeout value {0}";

        public string INST_ERR_MIXED_WRITE_DEFAULTS { get; private set; }
            = "Mixed write defaults configuration found on your avatar. This may cause problems.";

        public string INST_ERR_WD_MISMATCH { get; private set; }
            = "Write defaults configuration does not match existing animators on your avatar. This may cause problems.";

        public string INST_ERR_NO_EXP_PARAMS { get; private set; }
            = "No expression parameters found on your avatar.";

        public string INST_ERR_SYNCED_SECRET_PARAM { get; private set; }
            = "Please delete secret parameter [{0}] from your expression parameters.";

        public string INST_MENU_FULL { get; private set; } = "Selected submenu is full";

        #endregion
    }
}