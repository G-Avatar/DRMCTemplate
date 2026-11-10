import axios, { AxiosError, type AxiosResponse } from 'axios'
import { reactive } from 'vue'
import Cookies from 'js-cookie'
import type { IRequest } from '@/interfaces/global/IRequest'
import type { IResponse } from '@/interfaces/global/IResponse'
import Page from '@/stores/page'
axios.defaults.withCredentials = true
axios.defaults.withXSRFToken = true
axios.defaults.xsrfCookieName = 'X-XSRF-TOKEN'
axios.defaults.headers.common['X-Requested-With'] = 'XMLHttpRequest'
axios.defaults.baseURL = import.meta.env.VITE_API_URL
axios.defaults.timeout = 0

export default function useApiClient<Input, Output>() {
    const request: IRequest<Output> = reactive({
        loading: false,
    })

    const getCsrfCookie = async () => {
        if (!Cookies.get('XSRF-TOKEN') || !Cookies.get('X-XSRF-TOKEN')) {
            const expirationDate = new Date()
            expirationDate.setMinutes(expirationDate.getMinutes() + 120)
            await axios({
                url: '/api/v1/AntiForgery',
                method: 'GET',
            })
        }
    }

    const setResult = (response: AxiosResponse | AxiosError) => {
        if (response instanceof AxiosError) {
            if (response.code === 'ECONNABORTED' || response.code === 'ERR_NETWORK') {
                request.message = 'Please check your internet connection and try again'
                request.status = -1
            }
            if (response.response) {
                request.status = response.response?.status
                if (response.response.data) {
                    const res: IResponse<Output> = response.response.data
                    request.message = res.message
                    request.data = res.data
                    request.errors = res.errors as Record<string, string[]>
                    if (request.status === 401 && res.message === 'Unauthenticated.') {
                        Cookies.remove('XSRF-TOKEN')
                        Page.user = null
                    }
                }
            }
        } else {
            request.status = response.status
            request.message = response.data.message
            request.data = response.data.data
            request.errors = response.data.errors
        }
    }

    const post = async (url: string, data: Input, useApi: boolean = true) => {
        request.loading = true
        const settings = {
            url: useApi ? '/api/' + url : url,
            method: 'POST',
            data: data,
            headers: {
                'Content-Type': 'application/json',
                Accept: 'application/json',
            },
        }
        await getCsrfCookie()
        await axios(settings)
            .then((response) => {
                setResult(response)
            })
            .catch((error) => {
                setResult(error)
            })
            .finally(() => {
                request.loading = false
                axios.defaults.baseURL = import.meta.env.VITE_API_URL
            })
    }

    const get = async (url: string, query?: Input) => {
        request.loading = true
        await getCsrfCookie()
        await axios({
            url: '/api/' + url,
            method: 'GET',
            params: query,
            headers: {
                Accept: 'application/json',
            },
        })
            .then((response) => {
                setResult(response)
            })
            .catch((error) => {
                setResult(error)
            })
            .finally(() => {
                request.loading = false
            })
    }

    const patch = async (url: string, data: Input) => {
        request.loading = true
        await getCsrfCookie()
        await axios({
            url: '/api/' + url,
            method: 'PATCH',
            data: data,
            headers: {
                'Content-Type': 'application/json',
                Accept: 'application/json',
            },
        })
            .then((response) => {
                setResult(response)
            })
            .catch((error) => {
                setResult(error)
            })
            .finally(() => {
                request.loading = false
            })
    }

    const deleteRequest = async (url: string, data: Input) => {
        request.loading = true
        await getCsrfCookie()
        await axios({
            url: '/api/' + url,
            method: 'DELETE',
            data: data,
            headers: {
                'Content-Type': 'application/json',
                Accept: 'application/json',
            },
        })
            .then((response) => {
                setResult(response)
            })
            .catch((error) => {
                setResult(error)
            })
            .finally(() => {
                request.loading = false
            })
    }

    const postFormData = async (url: string, data: Input) => {
        request.percentage = 0
        request.loading = true
        await getCsrfCookie()
        await axios({
            url: '/api/' + url,
            method: 'POST',
            data: data,
            headers: {
                Accept: 'application/json',
                'Content-Type': 'multipart/form-data',
            },
            onUploadProgress: (event) => {
                if (event.total) {
                    request.percentage = Math.round(
                        (event.loaded / event.total) * 100
                    )
                }
            },
        })
            .then((response) => {
                setResult(response)
            })
            .catch((error) => {
                setResult(error)
            })
            .finally(() => {
                request.loading = false
            })
    }

    const patchFormData = async (url: string, data: Input) => {
        request.percentage = 0
        request.loading = true
        const form = data as FormData
        await getCsrfCookie()
        await axios({
            url: '/api/' + url,
            method: 'PATCH',
            data: form,
            headers: {
                Accept: 'application/json',
                'Content-Type': 'multipart/form-data',
            },
            onUploadProgress: (event) => {
                if (event.total) {
                    request.percentage = Math.round(
                        (event.loaded / event.total) * 100
                    )
                }
            },
        })
            .then((response) => {
                setResult(response)
            })
            .catch((error) => {
                setResult(error)
            })
            .finally(() => {
                request.loading = false
            })
    }

    const putFormData = async (url: string, data: Input) => {
        request.loading = true
        const form = data as FormData
        await getCsrfCookie()
        await axios({
            url: '/api/' + url,
            method: 'PUT',
            data: form,
            headers: {
                Accept: 'application/json',
            },
            onUploadProgress: (event) => {
                if (event.total) {
                    request.percentage = Math.round(
                        (event.loaded / event.total) * 100
                    )
                }
            },
        })
            .then((response) => {
                setResult(response)
            })
            .catch((error) => {
                setResult(error)
            })
            .finally(() => {
                request.loading = false
            })
    }

    const put = async (url: string, data: Input) => {
        request.loading = true
        await getCsrfCookie()
        await axios({
            url: '/api/' + url,
            method: 'PUT',
            data: data,
            headers: {
                Accept: 'application/json',
            },
        })
            .then((response) => {
                setResult(response)
            })
            .catch((error) => {
                setResult(error)
            })
            .finally(() => {
                request.loading = false
            })
    }

    const getFile = async (url: string) => {
        request.loading = true
        await getCsrfCookie()
        await axios({
            url: '/api/' + url,
            method: 'GET',
            responseType: 'blob',
        })
            .then((response) => {
                setResult(response)
                request.data = response.data
            })
            .catch(async (error) => {
                if (
                    error.response?.data instanceof Blob &&
                    error.response.data.type.includes('json')
                ) {
                    error.response.data = JSON.parse(await error.response.data.text())
                }
                setResult(error)
            })
            .finally(() => {
                request.loading = false
            })
    }

    return {
        request,
        post,
        get,
        patch,
        postFormData,
        deleteRequest,
        patchFormData,
        getFile,
        axios,
        getCsrfCookie,
        putFormData,
        put,
    }
}