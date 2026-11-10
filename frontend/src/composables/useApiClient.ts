import axios, { AxiosError, type AxiosProgressEvent, type AxiosRequestConfig } from 'axios'
import { reactive } from 'vue'
import type { IRequest } from '@/interfaces/global/IRequest'
import type { IResponse } from '@/interfaces/global/IResponse'
import { login } from '@/lib/auth'
import Page from '@/stores/page'

axios.defaults.withCredentials = true
axios.defaults.baseURL = import.meta.env.VITE_API_URL

// Send the same idempotencyKey when retrying one user action (from crypto.randomUUID()); make a new one after it succeeds.
const idempotency = (key?: string) => (key ? { 'Idempotency-Key': key } : {})

export default function useApiClient<Input, Output>() {
    const request: IRequest<Output> = reactive({
        loading: false,
    })

    // The API always answers { message, data, errors }; 204 and 401 have no body.
    const setResult = (status: number, body?: IResponse<Output>) => {
        request.status = status
        request.message = body?.message
        request.data = body?.data
        request.errors = body?.errors
    }

    const trackUpload = (event: AxiosProgressEvent) => {
        request.percentage = Math.round((event.progress ?? 0) * 100)
    }

    let controller: AbortController | undefined

    const send = async (config: AxiosRequestConfig) => {
        controller = new AbortController()
        request.loading = true
        request.percentage = 0
        try {
            const response = await axios<IResponse<Output>>({
                ...config,
                url: '/api/' + config.url,
                signal: controller.signal,
                // FormData only: an upload listener makes the browser send an extra CORS preflight on cross-origin requests.
                onUploadProgress: config.data instanceof FormData ? trackUpload : undefined,
            })
            setResult(response.status, response.data)
        } catch (error) {
            if (axios.isCancel(error)) {
                setResult(0, { message: 'Request cancelled.' })
                return
            }
            if (!(error instanceof AxiosError)) throw error
            setResult(error.response?.status ?? -1, error.response?.data)
            if (!error.response) request.message = 'Please check your internet connection and try again'
            if (error.response?.status === 401) {
                Page.user = null
                login(window.location.pathname + window.location.search)
            }
        } finally {
            request.loading = false
        }
    }

    return {
        request,
        cancel: () => controller?.abort(),
        get: (url: string, query?: Input) => send({ url, method: 'GET', params: query }),
        post: (url: string, data: Input, idempotencyKey?: string) =>
            send({ url, method: 'POST', data, headers: idempotency(idempotencyKey) }),
        put: (url: string, data: Input, idempotencyKey?: string) =>
            send({ url, method: 'PUT', data, headers: idempotency(idempotencyKey) }),
        patch: (url: string, data: Input, idempotencyKey?: string) =>
            send({ url, method: 'PATCH', data, headers: idempotency(idempotencyKey) }),
        deleteRequest: (url: string, data?: Input) => send({ url, method: 'DELETE', data }),
    }
}
