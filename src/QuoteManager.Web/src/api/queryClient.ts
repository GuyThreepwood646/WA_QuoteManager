import { QueryClient } from '@tanstack/react-query'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Quote lifecycle data is the kind of thing another user changes while you are looking at it,
      // so refetching on focus is a feature here rather than noise.
      refetchOnWindowFocus: true,
      staleTime: 10_000,
      retry: 1,
    },
    mutations: {
      // A rejected transition is a legitimate domain answer, never a transient fault, so
      // retrying it would only produce a second identical rejection.
      retry: false,
    },
  },
})
