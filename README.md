# TR.BVEConductorChecker
Check the Conductor's Actions in the BVE5/6

## About this
BVEから標準出力に出力される車掌に関する情報を監視し, 何か行動を起こした際にそれを通知する機能です.

C#で作成されたATSプラグインから呼ばれることを想定していますが, 将来的にはC++からの呼び出しにも対応する予定です.

## How to use
ATSプラグインよりインスタンスを初期化し, インスタンスの`ConductorActioned`イベントを購読してください.  なお, 複数インスタンスを生成した場合の挙動は未確認です.


