class FileUtils {
    static async read(url) {
      const res = await fetch(url)
      return await res.text()
    }
  }