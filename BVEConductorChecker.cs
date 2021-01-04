using System;
using System.IO;
using System.Text;

namespace TR
{
	/// <summary>車掌が確認したこと/操作したことの種類</summary>
	public enum ConductorActionType
	{
		/// <summary>不明な操作</summary>
		Unknown,
		/// <summary>発車ベル ON</summary>
		Bell_ON,
		/// <summary>発車ベル OFF</summary>
		Bell_OFF,

		/// <summary>停止位置よし</summary>
		StopPos_OK,

		/// <summary>ドアスイッチ "閉"</summary>
		CDS_ToClose_UnknownSide,
		/// <summary>ドアスイッチ "開"</summary>
		CDS_ToOpen_UnknownSide,

		/// <summary>側灯 "滅"</summary>
		SIL_Off_UnknownSide,
		/// <summary>側灯 "点灯"</summary>
		SID_On_UnknownSide,


		/// <summary>進行方向右側ドアスイッチ "閉"</summary>
		CDS_ToClose_RightSide,
		/// <summary>進行方向右側ドアスイッチ "開"</summary>
		CDS_ToOpen_RightSide,
		/// <summary>進行方向右側側灯 "滅"</summary>
		SIL_Off_RightSide,
		/// <summary>進行方向右側側灯 "点灯"</summary>
		SID_On_RightSide,

		/// <summary>進行方向左側ドアスイッチ "閉"</summary>
		CDS_ToClose_LeftSide,
		/// <summary>進行方向左側ドアスイッチ "開"</summary>
		CDS_ToOpen_LeftSide,

		/// <summary>進行方向左側側灯 "滅"</summary>
		SIL_Off_LeftSide,
		/// <summary>進行方向左側側灯 "点灯"</summary>
		SID_On_LeftSide,
	}

	/// <summary>車掌が何らかの操作を行った場合に発生するイベントの情報</summary>
	public class ConductorActionedEventArgs : EventArgs
	{
		public ConductorActionedEventArgs(in ConductorActionType cat, in string? rawString)
		{
			ActionType = cat;
			RawString = rawString;
		}

		/// <summary>車掌の情報</summary>
		public ConductorActionType ActionType { get; }

		/// <summary>生文字列</summary>
		public string? RawString { get; }
	}

	public class BVEConductorChecker : IDisposable
	{
		TextWriter StdOut_ConsoleSide { get; }
		StringWriter StdOut_BveSide { get; }
		StringBuilder StdOut_BveSide_StringBuilder { get; }

		/// <summary>BVEから受信した出力情報を再度標準出力に流すかどうか</summary>
		public bool DoRedirect { get; set; } = true;

		/// <summary>不明な情報出力を通知するかどうか</summary>
		public bool AllowUnknownCommand { get; set; } = false;

		/// <summary>車掌が何らかの行動を起こした場合に発火するイベント</summary>
		public event EventHandler<ConductorActionedEventArgs>? ConductorActioned;

		/// <summary>BVEからの情報出力の自動確認の間隔</summary>
		public int CheckRate { get; set; } = 10;

		/// <summary>BVEからの情報出力を自動確認するかどうか</summary>
		public bool DoAutoCheck { get; } = true;

		/// <summary>BVEからの情報出力を監視するバックグラウンドタスク</summary>
		protected IMyTask StdOut_BVESide_DataCheckTask { get; }

		/// <summary>標準入力から車掌の動きを確認します</summary>
		/// <param name="doAutoCheck">標準出力の監視を行うかどうか</param>
		/// <param name="doRedirect">BVEからの出力を標準出力に再送信するか</param>
		/// <param name="allowUnknownCommand">車掌に関係ない(識別不能な)コマンドも通知するかどうか</param>
		public BVEConductorChecker(in bool doAutoCheck = true, in bool doRedirect = true, in bool allowUnknownCommand = false)
		{
			DoAutoCheck = doAutoCheck;
			DoRedirect = doRedirect;
			AllowUnknownCommand = allowUnknownCommand;

			StdOut_ConsoleSide = Console.Out;
			StdOut_BveSide = new StringWriter();
			StdOut_BveSide_StringBuilder = StdOut_BveSide.GetStringBuilder();
			Console.SetOut(StdOut_BveSide);

			StdOut_BVESide_DataCheckTask = new MyTask((_) =>
			{
				while (!IsDisposing)
				{
					var c = CheckConductor();
					if (c is not null && c.RawString is not null)
					{
						if (AllowUnknownCommand //Unknown Commandを受理するなら, 常にイベントを発火させる
						|| c.ActionType != ConductorActionType.Unknown) //Unknown Commandを受理しない場合は, Unknown出ない場合に限りイベントを発火させる
							ConductorActioned?.Invoke(this, c);

						if (DoRedirect)
							StdOut_ConsoleSide.Write(c.RawString);
					}
					MyTask.Delay(CheckRate);
				}
			});

			StdOut_BVESide_DataCheckTask.Start();
		}

		protected virtual ConductorActionedEventArgs? CheckConductor()
		{
			if (StdOut_BveSide_StringBuilder.Length <= 0)
				return null;

			string s = StdOut_BveSide_StringBuilder.ToString();
			StdOut_BveSide_StringBuilder.Remove(0, StdOut_BveSide_StringBuilder.Length);//net20でClearを使えないため
			return new ConductorActionedEventArgs(CheckActionType(s.Replace(StdOut_BveSide.NewLine, string.Empty)), s);
		}

		public virtual ConductorActionType CheckActionType(in string s)
			=> s switch
			{
				"発車ベル: ON" => ConductorActionType.Bell_ON,
				"発車ベル: OFF" => ConductorActionType.Bell_OFF,//実装未確認
				"車掌: 停止位置よし" => ConductorActionType.StopPos_OK,
				"車掌スイッチ: 閉" => ConductorActionType.CDS_ToClose_UnknownSide,
				"車掌スイッチ: 開" => ConductorActionType.CDS_ToOpen_UnknownSide,
				"側灯滅" => ConductorActionType.SIL_Off_UnknownSide,
				_ => ConductorActionType.Unknown
			};

		#region IDisposable
		private bool disposedValue;
		public bool IsDisposing { get; private set; } = false;
		protected virtual void Dispose(bool disposing)
		{
			IsDisposing = true;
			if (!disposedValue)
			{
				if (disposing)
				{
					StdOut_BVESide_DataCheckTask.Wait(1000);
					StdOut_BveSide.Dispose();
					Console.SetOut(StdOut_ConsoleSide);//標準出力を復帰させる
				}

				disposedValue = true;
			}
		}

		// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		// ~BVEConductorChecker()
		// {
		//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		//     Dispose(disposing: false);
		// }

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
		#endregion
	}
}
