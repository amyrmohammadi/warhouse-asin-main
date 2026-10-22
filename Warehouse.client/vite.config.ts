import { spawnSync } from 'node:child_process'
import { existsSync, mkdirSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { env } from 'node:process'
import { fileURLToPath, URL } from 'node:url'

import plugin from '@vitejs/plugin-vue'
import { defineConfig, type ServerOptions } from 'vite'

function createDevelopmentServerOptions(): ServerOptions {
  const configuredAspNetUrl = env.ASPNETCORE_URLS?.split(';').find(Boolean)
  const target = 'http://localhost:5189'
  // env.ASPNETCORE_HTTPS_PORT
    // ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}`
    // : 
    // (configuredAspNetUrl ?? 'https://localhost:5189')
  const port = Number.parseInt(env.DEV_SERVER_PORT ?? '10243', 10)

  if (env.DEV_SERVER_HTTPS === 'false') {
    return {
      proxy: {
        '^/endpoint': { target, secure: false },
        '^/weatherforecast': { target, secure: false },
      },
      port,
    }
  }

  const baseFolder = env.APPDATA
    ? `${env.APPDATA}/ASP.NET/https`
    : env.HOME
      ? `${env.HOME}/.aspnet/https`
      : undefined

  if (!baseFolder) {
    throw new Error('Could not determine the ASP.NET HTTPS certificate directory.')
  }

  const certificateName = 'Warehouse.client'
  const certFilePath = path.join(baseFolder, `${certificateName}.pem`)
  const keyFilePath = path.join(baseFolder, `${certificateName}.key`)

  if (!existsSync(baseFolder)) {
    mkdirSync(baseFolder, { recursive: true })
  }

  if (!existsSync(certFilePath) || !existsSync(keyFilePath)) {
    const certificateResult = spawnSync(
      'dotnet',
      [
        'dev-certs',
        'https',
        '--export-path',
        certFilePath,
        '--format',
        'Pem',
        '--no-password',
      ],
      { stdio: 'inherit' },
    )

    if (certificateResult.status !== 0) {
      throw new Error('Could not create certificate.')
    }
  }

  return {
    proxy: {
      '^/endpoint': {
        target,
        secure: false,
      },
      '^/weatherforecast': {
        target,
        secure: false,
      },
    },
    port,
    https: {
      key: readFileSync(keyFilePath),
      cert: readFileSync(certFilePath),
    },
  }
}

export default defineConfig(({ command }) => ({
  plugins: [plugin()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: command === 'serve' ? createDevelopmentServerOptions() : undefined,
}))
