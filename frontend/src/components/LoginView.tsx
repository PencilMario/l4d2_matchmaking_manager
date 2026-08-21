import React, { useState } from 'react';
import { KeyRound, Eye, EyeOff, Loader2, ShieldCheck, AlertCircle } from 'lucide-react';

interface LoginViewProps {
  onLogin: (token: string) => Promise<boolean>;
  initialError?: string | null;
}

export const LoginView: React.FC<LoginViewProps> = ({ onLogin, initialError }) => {
  const [token, setToken] = useState('');
  const [showToken, setShowToken] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(initialError || null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!token.trim()) return;

    setIsLoading(true);
    setErrorMessage(null);

    try {
      const success = await onLogin(token.trim());
      if (!success) {
        setErrorMessage('访问令牌无效或控制服务未响应，请检查后重试。');
      }
    } catch {
      setErrorMessage('连接控制服务失败，请检查网络或服务状态。');
    } finally {
      setIsLoading(false);
    }
  };

  const handleUsePreset = () => {
    setToken('l4d-adm-secret');
    setErrorMessage(null);
  };

  return (
    <div className="min-h-screen bg-slate-100 flex items-center justify-center p-4">
      <div className="w-full max-w-md bg-white rounded-lg shadow-md border border-slate-200 p-8 space-y-6">
        {/* Header */}
        <div className="text-center space-y-2">
          <div className="inline-flex items-center justify-center w-12 h-12 rounded-lg bg-blue-50 text-blue-600 border border-blue-100 mb-1">
            <ShieldCheck className="w-6 h-6" />
          </div>
          <h1 className="text-xl font-bold text-slate-900 tracking-tight">
            L4D1 匹配管理
          </h1>
          <p className="text-xs text-slate-500">
            请输入控制服务访问令牌以进入管理后台
          </p>
        </div>

        {/* Error alert */}
        {errorMessage && (
          <div className="flex items-start gap-2.5 p-3 text-xs text-red-800 bg-red-50 border border-red-200 rounded-md">
            <AlertCircle className="w-4 h-4 text-red-600 shrink-0 mt-0.5" />
            <span className="leading-relaxed">{errorMessage}</span>
          </div>
        )}

        {/* Form */}
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label
              htmlFor="access-token-input"
              className="block text-xs font-medium text-slate-700 mb-1.5"
            >
              访问令牌
            </label>
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-400">
                <KeyRound className="w-4 h-4" />
              </div>
              <input
                id="access-token-input"
                type={showToken ? 'text' : 'password'}
                value={token}
                onChange={(e) => setToken(e.target.value)}
                placeholder="请输入 Bearer 访问令牌"
                disabled={isLoading}
                autoFocus
                className="w-full pl-9 pr-10 py-2 text-sm bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500 focus:border-blue-500 disabled:bg-slate-50 text-slate-900 placeholder:text-slate-400 transition-colors"
              />
              <button
                type="button"
                onClick={() => setShowToken(!showToken)}
                aria-label={showToken ? '隐藏令牌内容' : '显示令牌内容'}
                className="absolute inset-y-0 right-0 pr-3 flex items-center text-slate-400 hover:text-slate-600 focus:outline-hidden"
              >
                {showToken ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
          </div>

          <button
            type="submit"
            disabled={!token.trim() || isLoading}
            className="w-full flex items-center justify-center gap-2 py-2.5 px-4 text-xs font-medium text-white bg-blue-600 hover:bg-blue-700 active:bg-blue-800 rounded-md shadow-xs focus:outline-hidden focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            {isLoading ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
                <span>正在连接</span>
              </>
            ) : (
              <span>进入控制台</span>
            )}
          </button>
        </form>

        {/* Quick test preset */}
        <div className="pt-4 border-t border-slate-100 flex items-center justify-between text-xs text-slate-500">
          <span>测试环境快捷凭据：</span>
          <button
            type="button"
            onClick={handleUsePreset}
            className="text-blue-600 hover:text-blue-700 hover:underline font-mono text-[11px]"
          >
            填入默认测试令牌
          </button>
        </div>
      </div>
    </div>
  );
};
