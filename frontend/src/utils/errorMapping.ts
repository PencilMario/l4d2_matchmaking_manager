export interface ApiErrorResponse {
  error?: string;
  code?: string;
  message?: string;
}

export function mapErrorMessage(err: unknown, statusCode?: number): string {
  if (typeof err === 'string') {
    if (err.includes('target_server_drain_failed')) {
      return '相关暖服任务未能全部停止，操作尚未完成，请检查任务状态后重试。';
    }
    return err;
  }

  if (statusCode === 409 || (err && typeof err === 'object' && ('code' in err) && (err as ApiErrorResponse).code === 'target_server_drain_failed')) {
    return '相关暖服任务未能全部停止，操作尚未完成，请检查任务状态后重试。';
  }

  if (statusCode === 401) {
    return '访问令牌无效或已过期，请重新登录。';
  }

  if (statusCode === 403) {
    return '无权执行该操作，请核实当前令牌权限。';
  }

  if (statusCode === 404) {
    return '请求的资源不存在或已被移除。';
  }

  if (err && typeof err === 'object') {
    const apiErr = err as ApiErrorResponse;
    if (apiErr.code === 'target_server_drain_failed') {
      return '相关暖服任务未能全部停止，操作尚未完成，请检查任务状态后重试。';
    }
    if (apiErr.message) {
      if (apiErr.message.includes('target_server_drain_failed')) {
        return '相关暖服任务未能全部停止，操作尚未完成，请检查任务状态后重试。';
      }
      return apiErr.message;
    }
    if (apiErr.error) {
      return apiErr.error;
    }
  }

  if (statusCode && statusCode >= 500) {
    return '控制服务内部异常，请稍后重试或检查控制服务状态。';
  }

  return '操作失败，请检查网络连接或控制服务状态后重试。';
}
