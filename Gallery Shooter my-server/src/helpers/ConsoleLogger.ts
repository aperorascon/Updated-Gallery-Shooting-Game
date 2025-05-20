
export class ConsoleLogger {
  static info(message: string) {
    console.log(`[INFO] ${new Date().toISOString()} - ${message}`);
  }

  static error(message: string) {
    console.error(`[ERROR] ${new Date().toISOString()} - ${message}`);
    } static debug(message: string) {
        console.debug(`[Debug] ${new Date().toISOString()} - ${message}`);
    }

  // Add other methods as needed
}

export default ConsoleLogger;