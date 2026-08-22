import { useState, type FormEvent } from 'react';
import type {
  WarmupPauseWindow,
  WarmupPauseWindowsSettings,
  WarmupPauseWindowsSettingsInput,
} from '../../api/models';
import { describeError } from '../../state/display';

const TIME_PATTERN = /^(?:[01]\d|2[0-3]):[0-5]\d$/;

type Props = {
  initialWindows: WarmupPauseWindow[];
  initialActive: boolean;
  onSave: (input: WarmupPauseWindowsSettingsInput) => Promise<WarmupPauseWindowsSettings>;
};

export function WarmupPauseWindowsForm({ initialWindows, initialActive, onSave }: Props) {
  const [windows, setWindows] = useState<WarmupPauseWindow[]>(() => cloneWindows(initialWindows));
  const [active, setActive] = useState(initialActive);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const updateWindow = (index: number, field: keyof WarmupPauseWindow, value: string) => {
    setWindows(current => current.map((window, currentIndex) => currentIndex === index ? { ...window, [field]: value } : window));
    setError(null);
    setMessage(null);
  };

  const addWindow = () => {
    setWindows(current => [...current, { start: '', end: '' }]);
    setError(null);
    setMessage(null);
  };

  const removeWindow = (index: number) => {
    setWindows(current => current.filter((_, currentIndex) => currentIndex !== index));
    setError(null);
    setMessage(null);
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const validationError = validateWindows(windows);
    if (validationError) {
      setError(validationError);
      setMessage(null);
      return;
    }

    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const value = await onSave({ windows: cloneWindows(windows) });
      setWindows(cloneWindows(value.windows));
      setActive(value.active);
      setMessage('暖服暂停时间段已保存。');
    } catch (caught) {
      setError(describeError(caught).message);
    } finally {
      setSaving(false);
    }
  };

  return <form className="pause-window-form" onSubmit={submit}>
    <div className="pause-window-form__status" aria-live="polite">
      <span className={`settings-status ${active ? 'settings-status--active' : 'settings-status--idle'}`}>
        <span className="settings-status__dot" aria-hidden="true" />
        当前状态：{active ? '正在暂停' : '未暂停'}
      </span>
      <span className="pause-window-form__timezone">Asia/Shanghai · UTC+8 · 精确到分钟</span>
    </div>
    <p className="pause-window-form__hint">暂停区间为左闭右开，支持跨午夜；例如 23:00–00:00。时间段结束后自动恢复调度，暖服节点容器不会停止。</p>
    {windows.length === 0 ? <p className="pause-window-form__empty">暂未设置暂停时间段</p> : <div className="pause-window-list">
      {windows.map((window, index) => <div className="pause-window-row" key={`${index}-${window.start}-${window.end}`}>
        <label className="pause-window-row__field">开始时间 {index + 1}
          <input aria-label={`开始时间 ${index + 1}`} onChange={event => updateWindow(index, 'start', event.target.value)} type="time" value={window.start} />
        </label>
        <span className="pause-window-row__separator" aria-hidden="true">至</span>
        <label className="pause-window-row__field">结束时间 {index + 1}
          <input aria-label={`结束时间 ${index + 1}`} onChange={event => updateWindow(index, 'end', event.target.value)} type="time" value={window.end} />
        </label>
        <button aria-label={`删除时间段 ${index + 1}`} className="button button--quiet pause-window-row__remove" disabled={saving} onClick={() => removeWindow(index)} type="button">删除</button>
      </div>)}
    </div>}
    {error && <p className="inline-error" role="alert">{error}</p>}
    {message && <p className="success-message" role="status">{message}</p>}
    <div className="pause-window-form__actions">
      <button className="button button--quiet" disabled={saving} onClick={addWindow} type="button">新增时间段</button>
      <button className="button button--primary" disabled={saving} type="submit">{saving ? '正在保存' : '保存暖服暂停时间段'}</button>
    </div>
  </form>;
}

function cloneWindows(windows: WarmupPauseWindow[]) {
  return windows.map(window => ({ start: window.start, end: window.end }));
}

function validateWindows(windows: WarmupPauseWindow[]) {
  for (const window of windows) {
    if (!TIME_PATTERN.test(window.start) || !TIME_PATTERN.test(window.end)) {
      return '请填写完整的时间段。';
    }
    if (window.start === window.end) {
      return '开始和结束时间不能相同。';
    }
  }
  return null;
}
