const { screen } = require('electron');

// Mock robot functionality for environments where robotjs isn't available
let robot;
try {
  robot = require('robotjs');
} catch (error) {
  console.warn('robotjs not available, input forwarding will be simulated');
  robot = {
    moveMouse: (x, y) => console.log(`Mock: Move mouse to ${x}, ${y}`),
    mouseClick: (button, doubleClick) => console.log(`Mock: Mouse click ${button}`),
    keyTap: (key, modifiers) => console.log(`Mock: Key tap ${key} with modifiers ${modifiers}`),
    scrollMouse: (x, y) => console.log(`Mock: Scroll mouse ${x}, ${y}`),
    getMousePos: () => ({ x: 100, y: 100 })
  };
}

class InputHandler {
  constructor() {
    this.currentDisplay = null;
    this.isEnabled = true;
  }

  setDisplay(display) {
    this.currentDisplay = display;
    console.log('Input handler set to display:', display.id);
  }

  enable() {
    this.isEnabled = true;
    console.log('Input handler enabled');
  }

  disable() {
    this.isEnabled = false;
    console.log('Input handler disabled');
  }

  handleInputEvent(event) {
    if (!this.isEnabled || !this.currentDisplay) {
      return;
    }

    try {
      switch (event.eventType) {
        case 'mouse':
          this.handleMouseEvent(event);
          break;
        case 'touch':
          this.handleTouchEvent(event);
          break;
        case 'keyboard':
          this.handleKeyboardEvent(event);
          break;
        case 'scroll':
          this.handleScrollEvent(event);
          break;
        default:
          console.warn('Unknown input event type:', event.eventType);
      }
    } catch (error) {
      console.error('Error handling input event:', error);
    }
  }

  handleMouseEvent(event) {
    const { action, x, y, button } = event;
    
    // Convert relative coordinates to absolute screen coordinates
    const absoluteX = this.currentDisplay.bounds.x + (x * this.currentDisplay.bounds.width);
    const absoluteY = this.currentDisplay.bounds.y + (y * this.currentDisplay.bounds.height);

    robot.moveMouse(absoluteX, absoluteY);

    if (action === 'down') {
      const robotButton = this.convertButton(button);
      robot.mouseClick(robotButton, false); // false = don't double click
    } else if (action === 'up') {
      // Mouse up is handled automatically by robotjs
    }
  }

  handleTouchEvent(event) {
    // Convert touch events to mouse events for desktop interaction
    const { action, x, y } = event;
    
    const absoluteX = this.currentDisplay.bounds.x + (x * this.currentDisplay.bounds.width);
    const absoluteY = this.currentDisplay.bounds.y + (y * this.currentDisplay.bounds.height);

    robot.moveMouse(absoluteX, absoluteY);

    if (action === 'start') {
      robot.mouseClick('left', false);
    } else if (action === 'end') {
      // Touch end - no additional action needed
    }
  }

  handleKeyboardEvent(event) {
    const { action, key, modifiers = [] } = event;

    if (action === 'down') {
      // Handle modifier keys
      const robotModifiers = modifiers.map(mod => this.convertModifier(mod)).filter(Boolean);
      
      if (robotModifiers.length > 0) {
        robot.keyTap(key, robotModifiers);
      } else {
        robot.keyTap(key);
      }
    }
  }

  handleScrollEvent(event) {
    const { x, y, deltaX, deltaY } = event;
    
    // Move mouse to scroll position
    const absoluteX = this.currentDisplay.bounds.x + (x * this.currentDisplay.bounds.width);
    const absoluteY = this.currentDisplay.bounds.y + (y * this.currentDisplay.bounds.height);
    
    robot.moveMouse(absoluteX, absoluteY);

    // Perform scroll
    // RobotJS scroll values are inverted compared to web standards
    if (Math.abs(deltaY) > Math.abs(deltaX)) {
      robot.scrollMouse(0, -deltaY / 100); // Vertical scroll
    } else {
      robot.scrollMouse(-deltaX / 100, 0); // Horizontal scroll
    }
  }

  convertButton(button) {
    switch (button) {
      case 0:
      case 'left':
        return 'left';
      case 1:
      case 'middle':
        return 'middle';
      case 2:
      case 'right':
        return 'right';
      default:
        return 'left';
    }
  }

  convertModifier(modifier) {
    switch (modifier.toLowerCase()) {
      case 'ctrl':
      case 'control':
        return 'control';
      case 'alt':
        return 'alt';
      case 'shift':
        return 'shift';
      case 'meta':
      case 'cmd':
      case 'command':
        return process.platform === 'darwin' ? 'command' : 'control';
      default:
        return null;
    }
  }

  // Test input functionality
  testInput() {
    console.log('Testing input functionality...');
    
    try {
      // Get current mouse position
      const mouse = robot.getMousePos();
      console.log('Current mouse position:', mouse);

      // Test mouse movement
      robot.moveMouse(mouse.x + 10, mouse.y + 10);
      setTimeout(() => {
        robot.moveMouse(mouse.x, mouse.y);
        console.log('Input test completed successfully');
      }, 100);

      return true;
    } catch (error) {
      console.error('Input test failed:', error);
      return false;
    }
  }

  getStatus() {
    return {
      isEnabled: this.isEnabled,
      currentDisplay: this.currentDisplay ? this.currentDisplay.id : null,
      robotjsAvailable: robot && typeof robot.moveMouse === 'function'
    };
  }
}

module.exports = InputHandler;