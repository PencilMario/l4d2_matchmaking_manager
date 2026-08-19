import React from 'react';
import { Loader2 } from 'lucide-react';

export const LoadingView: React.FC = () => {
  return (
    <div className="min-h-screen bg-slate-100 flex flex-col items-center justify-center p-4">
      <div className="bg-white p-8 rounded-lg shadow-sm border border-slate-200 flex flex-col items-center max-w-sm w-full text-center space-y-4">
        <Loader2 className="w-8 h-8 text-blue-600 animate-spin" />
        <div className="space-y-1">
          <h2 className="text-sm font-semibold text-slate-800">
            正在读取控制服务数据
          </h2>
          <p className="text-xs text-slate-500">
            正在同步目标服务器、暖服节点、暖服任务及观测状态...
          </p>
        </div>
      </div>
    </div>
  );
};
